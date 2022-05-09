namespace QueueTasks.Controllers.Api.V1
{
    using System;
    using System.ComponentModel;
    using System.Threading;
    using System.Threading.Tasks;

    using Abstractions;

    using Models;

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
        [Description("Метод получения id свободной задачи и назначения на оператора")]
        [Consumes("application/json")]
        [Produces("application/json")]
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

            var timer = GetTimer();

            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();

            var channel = _queueOperatorManager.AddToQueue(operatorId);
            try
            {
                var task = await channel.Reader.ReadAsync(HttpContext.RequestAborted);

                await SendEventSse($"event: task\n" +
                                   $"data: {JsonConvert.SerializeObject(task)}\n\n");

                channel.Writer.Complete();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Выход из очереди");
                channel.Writer.Complete();
                _queueOperatorManager.Remove(operatorId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при ожидании появления задачи из очереди");
                channel.Writer.Complete();
                _queueOperatorManager.Remove(operatorId);
                throw;
            }
            finally
            {
                _pool.Dispose();
                timer.Stop();
                timer.Dispose();
            }

            return new EmptyResult();
        }

        private Timer GetTimer()
        {
            var timer = new Timer
            {
                Interval = 35 * 1000
            };
            timer.Elapsed +=
                async (sender, e) => await SendEventSse("event: connection\n" +
                                                        "data: \"Повторное подключение\"\n\n");
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
            return timer;
        }

        private readonly SemaphoreSlim _pool = new SemaphoreSlim(1, 1);

        private async Task SendEventSse(string @event)
        {
            await _pool.WaitAsync();
            if (!Response.HttpContext.RequestAborted.IsCancellationRequested)
            {
                await Response.WriteAsync(@event);
                await Response.Body.FlushAsync();
            }
            _pool.Release();
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
                _logger.LogWarning($"Задача {taskId} не назначена на оператора {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await _extensionService.TryAssignTask(taskId, operatorId);
            if (result.Success)
            {
                _queueOperatorManager.Remove(operatorId);
                return Ok();
            }

            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.CreateFailure(result.Error, (int)ErrorCodes.ApplicationAlreadyAssigned));
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
                _logger.LogWarning($"Задача {taskId} не назначена на оператора {operatorId}");
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            _queueOperatorManager.Remove(operatorId);
            await _tasksManager.AddTask(taskId);

            return Ok();
        }

        /// <summary>
        ///     Выход из очереди оператора (Удаление оператора из очереди)
        /// </summary>
        /// <returns></returns>
        [HttpDelete("exit")]
        public async Task<IActionResult> ExitFromQueue()
        {
            var operatorId = await _currentOperatorProvider.GetCurrentOperatorId();
            _queueOperatorManager.Remove(operatorId);
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
