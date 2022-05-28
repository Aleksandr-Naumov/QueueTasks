namespace QueueTasks.Api.V1
{
    using System;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using Abstractions;

    using Models;

    using Contracts;

    using QueueTasks.Models;

    using QueueTasks.Api.Models.Enums;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Newtonsoft.Json;

    using Timer = System.Timers.Timer;
    using QueueTasks.Exceptions;

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
        [Produces("application/json")]
        public async Task<IActionResult> TakeFreeTask()
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            if (!await _extensionService.CanAddToQueue(operatorId))
            {
                _logger.LogInformation($"Оператор {operatorId} не может встать в очередь из-за проверок ограничения на взятия и ожидания задач");
                return BadRequest(ApiResponse.CreateFailure("Нет возможности брать задачи"));
            }

            if (!_queueOperatorManager.IsEmpty())
            {
                return Ok(ApiResponse.CreateSuccess(new { TaskId = (string)default! }));
            }

            var taskId = await _extensionService.GetFreeTaskId(operatorId);
            return Ok(ApiResponse.CreateSuccess(new { TaskId = taskId }));
        }

        /// <summary>
        ///     Метод для вставки оператора в очередь и ожидания появления заявки из очереди.
        /// </summary>
        [HttpGet("wait-sse")]
        public async Task<IActionResult> WaitTask()
        {
            Response.Headers.Add("Content-Type", "text/event-stream; charset=UTF-8");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Access-Control-Allow-Origin", "*");

            string? operatorId = null;
            Channel<TaskFromChannel>? channel = null;
            Timer? timer = null;
            SemaphoreSlim? pool = null;
            try
            {
                pool = new SemaphoreSlim(1, 1);

                timer = new Timer
                {
                    Interval = 20 * 1000
                };
                timer.Elapsed +=
                    async (sender, e) => await SendEventSse("event: connection\n" +
                                                            "data: \"Повторное подключение\"\n\n", pool);
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();

                operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

                if (!await _extensionService.CanAddToQueue(operatorId))
                {
                    //TODO: или сделать event отдельный для такого случая
                    throw new PossibilityWaitTasksException();
                }

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

                CompletingWaitingTask(operatorId, channel, timer);
            }
            catch (PossibilityWaitTasksException)
            {
                _logger.LogInformation($"Оператор {operatorId} не может встать в очередь из-за проверок ограничения на взятия и ожидания задач");

                CompletingWaitingTask(operatorId, channel, timer);

                Response.StatusCode = 204;
                await Response.Body.FlushAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Ошибка при ожидании появления задачи из очереди оператором {operatorId}");

                CompletingWaitingTask(operatorId, channel, timer);
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

        private void CompletingWaitingTask(string? operatorId, Channel<TaskFromChannel>? channel, Timer? timer)
        {
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
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.CreateFailure("Не возможность назначения задачи на текущего оператора"));
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
            return Ok(ApiResponse.CreateSuccess());
        }

        /// <summary>
        ///     Метод для отказа от заявки (выход оператора из очереди и передача заявки другому оператору).
        /// </summary>
        /// <param name="taskId">Id задачи от которой отказался оператор</param>
        [HttpPost("{taskId}/reject")]
        [Produces("application/json")]
        public async Task<IActionResult> Reject(string taskId)
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            if (!await _queueOperatorManager.IsTaskForOperator(taskId, operatorId))
            {
                _logger.LogWarning($"Задача {taskId} не является потенциальной для отмены оператором {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse.CreateFailure("Не возможность отказа от задачи текущим оператором"));
            }

            _queueOperatorManager.Remove(operatorId);
            await _tasksManager.AddTask(taskId);
            _logger.LogInformation($"Задача {taskId} успешно назначена на оператора {operatorId}");
            return Ok(ApiResponse.CreateSuccess());
        }

        /// <summary>
        ///     Выход из очереди оператора - удаление оператора из очереди и если его вызвать, то получение задач в других вкладках не произойдет
        ///     (этот метод излишний, можно использовать только для тестирования, тк если нужно выйти из очереди можно закрыть подключение "wait-sse".
        ///     Также если будет несколько вкладок с открытым запросом "wait-sse", то при вызове этого метода подключение обрубится на этих вкладках и
        ///     "wait-sse" снова вызовется на этих вкладках, тк eventsource после неудачи переподключается, поэтому этот метод не особо эффективный)
        /// </summary>
        /// <returns></returns>
        [HttpDelete("exit")]
        [Produces("application/json")]
        public async Task<IActionResult> ExitFromQueue()
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();
            _queueOperatorManager.RemoveAll(operatorId);
            _logger.LogInformation($"Выход из очереди оператора {operatorId} через метод \"exit\"");
            return Ok(ApiResponse.CreateSuccess());
        }

        /// <summary>
        ///     Метод для получения списка очереди из операторов
        /// </summary>
        [HttpGet("queue-operators")]
        [Produces("application/json")]
        public IActionResult GetQueueOperators() =>
            Ok(ApiResponse.CreateSuccess(_queueOperatorManager.GetOperators()));
    }
}
