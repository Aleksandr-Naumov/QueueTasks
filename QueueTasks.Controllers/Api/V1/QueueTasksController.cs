namespace QueueTasks.Controllers.Api.V1
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using Abstractions;

    using Models;

    using QueueTasks.Models;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    using Timer = System.Timers.Timer;

    [Authorize]
    [ApiVersion("1")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class QueueTasksController : ControllerBase
    {
        private readonly ICurrentOperatorProvider _currentOperatorProvider;
        private readonly IExtensionService _extensionService;
        private readonly IQueueOperatorManager _queueOperatorManager;
        private readonly ITasksManager _tasksManager;
        private readonly ILogger<QueueTasksController> _logger;

        public QueueTasksController(
            ICurrentOperatorProvider currentOperatorProvider,
            IQueueOperatorManager queueOperatorManager,
            IExtensionService extensionService,
            ITasksManager tasksManager,
            ILogger<QueueTasksController> logger)
        {
            _currentOperatorProvider = currentOperatorProvider;
            _queueOperatorManager = queueOperatorManager;
            _extensionService = extensionService;
            _tasksManager = tasksManager;
            _logger = logger;
        }

        /// <summary>
        ///     Метод получения id задачи среди всех свободных, если нет операторов в очередь взятия задач,
        ///     и назначение задачи на оператора, если она была найдена.
        /// </summary>
        /// <returns>Id приоритетной свободной задачи или null, если таких нет или если есть операторы в очереди для взятия задач.</returns>
        /// <response code="200">Отправлена Id задачи, которая уже назначена на оператора</response>
        /// <response code="403">Потенциальная задача уже назначена на другого оператора</response>
        [HttpPost("take-free")]
        public async Task<IActionResult> TakeFreeTask()
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            if (!_queueOperatorManager.IsEmpty())
            {
                return Ok(new { TaskId = (string)default! });
            }

            var taskId = await _extensionService.GetFreeTaskId(operatorId);
            return Ok(new { TaskId = taskId });
        }

        /// <summary>
        ///     Метод для вставки оператора в очередь и ожидания появления заявки из очереди.
        /// </summary>
        [HttpGet("wait-sse")]
        public async Task<IActionResult> WaitTask()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            Response.StatusCode = 200;
            await Response.Body.FlushAsync();

            string? operatorId = null;
            Channel<TaskFromChannel>? channel = null;
            Timer? timer = null;
            SemaphoreSlim? pool = null;
            try
            {
                pool = new SemaphoreSlim(1, 1);

                timer = new Timer
                {
                    Interval = 25 * 1000
                };
                timer.Elapsed +=
                    async (sender, e) => await SendEventSse("event: connection\n" +
                                                            "data: \"Повторное подключение\"\n\n", pool);
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();

                operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

                channel = _queueOperatorManager.AddToQueue(operatorId);

                var taskFromChannel = await channel.Reader.ReadAsync(HttpContext.RequestAborted);

                channel.Writer.Complete();
                timer.Stop();

                await SendEventSse($"event: task\n" +
                                   $"data: {JsonConvert.SerializeObject(taskFromChannel)}\n\n", pool);

                _logger.LogInformation($"Оператору {operatorId} пришла задача {taskFromChannel.TaskId}");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Выход из очереди оператора {operatorId}");

                if (channel != null)
                {
                    channel.Writer.Complete();
                }
                if (timer != null)
                {
                    timer.Stop();
                }
                if (operatorId != null)
                {
                    _queueOperatorManager.Remove(operatorId);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Ошибка при ожидании появления задачи из очереди оператором {operatorId}");

                if (channel != null)
                {
                    channel.Writer.Complete();
                }
                if (timer != null)
                {
                    timer.Stop();
                }
                if (operatorId != null)
                {
                    _queueOperatorManager.Remove(operatorId);
                }
                throw;
            }
            finally
            {
                if (pool != null)
                {
                    pool.Dispose();
                }
                if (timer != null)
                {
                    timer.Dispose();
                }
            }

            return new EmptyResult();
        }

        private async Task SendEventSse(string @event, SemaphoreSlim pool)
        {
            await pool.WaitAsync();

            try
            {
                if (!Response.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    await Response.WriteAsync(@event);
                    await Response.Body.FlushAsync();
                }
            }
            finally
            {
                pool.Release();
            }
        }

        /// <summary>
        ///     Метод для назначения задачи на текущего оператора
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <response code="200">Задача назначена на оператора</response>
        /// <response code="403">
        ///     Потенциальная задача уже назначена на другого оператора и текущего оператора нужно поставить на ожидание новой задачи</response>
        /// <returns></returns>
        [HttpPost("{taskId}/assign")]
        [Produces("application/json")]
        public async Task<IActionResult> Assign(string taskId)
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            if (!await _queueOperatorManager.IsTaskForOperator(taskId, operatorId))
            {
                _logger.LogWarning($"Задача {taskId} не является потенциальной для назначения оператором {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await _extensionService.TryAssignTask(taskId, operatorId);
            if (!result.Success)
            {
                _logger.LogInformation($"Задачу {taskId} не получилось назначить на оператора {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.CreateFailure(result.Error, (int)ErrorCodes.TaskAlreadyAssigned));
            }

            _queueOperatorManager.Remove(operatorId);
            _logger.LogInformation($"Задача {taskId} успешно назначена на оператора {operatorId}");
            return Ok();
        }

        /// <summary>
        ///     Метод для отказа от заявки (выход оператора из очереди и передача заявки другому оператору).
        /// </summary>
        /// <param name="taskId">Id задачи от которой отказался оператор</param>
        [HttpPost("{taskId}/reject")]
        public async Task<IActionResult> Reject(string taskId)
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            if (!await _queueOperatorManager.IsTaskForOperator(taskId, operatorId))
            {
                _logger.LogWarning($"Задача {taskId} не является потенциальной для отмены оператором {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            _queueOperatorManager.Remove(operatorId);
            await _tasksManager.AddTask(taskId);
            _logger.LogInformation($"Задача {taskId} успешно назначена на оператора {operatorId}");
            return Ok();
        }

        /// <summary>
        ///     Выход из очереди оператора - удаление оператора из очереди и если его вызвать, то получение задач в других вкладках не произойдет
        ///     (этот метод излишний, можно использовать только для тестирования, тк если нужно выйти из очереди можно закрыть подключение "wait-sse".
        ///     Также если будет несколько вкладок с открытым запросом "wait-sse", то при вызове этого метода подключение обрубится на этих вкладках и
        ///     "wait-sse" снова вызовется на этих вкладках, тк eventsource после неудачи переподключается, поэтому этот метод не особо эффективный)
        /// </summary>
        /// <returns></returns>
        [HttpDelete("exit")]
        public async Task<IActionResult> ExitFromQueue()
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();
            _queueOperatorManager.RemoveAll(operatorId);
            _logger.LogInformation($"Выход из очереди оператора {operatorId} через метод \"exit\"");
            return Ok();
        }

        /// <summary>
        ///     Метод для получения списка очереди из операторов
        /// </summary>
        [HttpGet("queue-operators")]
        public IActionResult GetQueueOperators() =>
            Ok(_queueOperatorManager.GetOperators());
    }
}
