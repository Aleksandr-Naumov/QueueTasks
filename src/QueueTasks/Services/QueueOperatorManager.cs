namespace QueueTasks.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using System.Text.Json;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Abstractions;
    using Models;
    using Contracts;

    internal class QueueOperatorManager : IQueueOperatorManager, ITasksManager, IDisposable
    {
        private readonly ChannelService _channelService = new ChannelService();
        private readonly QueueOperators _queue = new QueueOperators();
        private readonly TimerForAssignTasks _timerForAssignTasks;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QueueOperatorManager> _logger;

        public QueueOperatorManager(
            IServiceProvider serviceProvider,
            ILogger<QueueOperatorManager> logger)
        {
            _timerForAssignTasks = new TimerForAssignTasks(this);
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task AddTask(string taskId)
        {
            _logger.LogInformation($"Пришла не назначенная задача {taskId}");

            var freeOperatorId = await GetFreeOperatorId(taskId);
            if (freeOperatorId == null)
            {
                return;
            }

            await _timerForAssignTasks.Add(taskId, freeOperatorId);

            _queue.ChangeStatusToSelects(freeOperatorId);
            await _channelService.WriteToChannel(taskId, freeOperatorId, assigned: false);
        }

        public async Task AddTask(string taskId, string operatorId)
        {
            _logger.LogInformation($"Пришла назначенная задача {taskId} на оператора {operatorId}");

            await _channelService.WriteToChannel(taskId, operatorId, assigned: true);
            // Могут не успеть закрыться все каналы и удаление оператора не произойдет
            Remove(operatorId);
        }

        public Channel<TaskFromChannel> AddToQueue(string operatorId)
        {
            var channel = _channelService.CreateChannel(operatorId);

            if (_queue.TryEnqueue(operatorId))
            {
                _logger.LogInformation($"Оператор {operatorId} добавлен в очередь");
            }
            else
            {
                _logger.LogInformation($"Оператор {operatorId} уже был добавлен в очередь");

                // Сменить статус нужно чтобы оператору досталась следующая новая задача,
                // если потенциальную задачу назначали на другого оператора (оператор также остался первым в очереди)
                _queue.ChangeStatusToFree(operatorId);
            }

            return channel;
        }

        public async Task<string?> GetFreeTaskIdAndAssign(string operatorId)
        {
            if (_queue.Contains(operatorId))
            {
                return null;
            }

            AssignResult? result = null;
            string? taskId;
            using (var serviceScope = _serviceProvider.CreateScope())
            {
                var extensionService = serviceScope.ServiceProvider.GetRequiredService<IExtensionService>();

                taskId = await extensionService.GetFreeTaskIdForOperator(operatorId);
                while (result == null || !result.Success)
                {
                    if (string.IsNullOrEmpty(taskId))
                    {
                        return null;
                    }

                    if (!_timerForAssignTasks.Contains(taskId) && await extensionService.CanAssign(taskId, operatorId))
                    {
                        result = await extensionService.TryAssignTask(taskId, operatorId);
                        if (!result.Success)
                        {
                            _logger.LogInformation($"Задача {taskId} не была назначена на оператора {operatorId}. Текст ошибки - {result.Error}");
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"Задачу {taskId} нельзя назначить на оператора {operatorId} или задача уже выдана другому оператору");
                    }

                    if (result == null || !result.Success)
                    {
                        taskId = await extensionService.GetFreeTaskIdForOperator(taskId, operatorId);
                    }
                }
            }

            return taskId;
        }

        public void Remove(string operatorId)
        {
            var channels = _channelService.GetChannels(operatorId);
            if (channels != null && channels!.All(x => x.Reader.Completion.IsCompleted))
            {
                RemoveFromQueue(operatorId);
                _channelService.Remove(operatorId);
            }
        }

        public void RemoveAll(string operatorId)
        {
            RemoveFromQueue(operatorId);

            var channels = _channelService.GetChannels(operatorId);
            if (channels != null)
            {
                foreach (var channel in channels)
                {
                    if (!channel.Reader.Completion.IsCompleted)
                    {
                        channel.Writer.Complete();
                    }
                }
            }

            _channelService.Remove(operatorId);
        }

        public async Task<bool> IsTaskForOperator(string taskId, string operatorId)
        {
            if (_timerForAssignTasks.CheckOperator(taskId, operatorId))
            {
                await _timerForAssignTasks.Remove(taskId);
                return true;
            }

            return false;
        }

        public IEnumerable<OperatorDto> GetOperators() => _queue.GetOperators();


        /// <summary>
        ///     Получить Id свободного оператора из очереди, на которого можно назначить данную задачу.
        /// </summary>
        private async Task<string?> GetFreeOperatorId(string taskId)
        {
            var firstOperatorId = GetFirstOperatorId();
            if (firstOperatorId == null)
            {
                _logger.LogInformation($"Задача не назначилась, тк нет операторов в очереди. Очередь - {JsonSerializer.Serialize(_queue.GetOperators())}");
                return null;
            }

            using (var serviceScope = _serviceProvider.CreateScope())
            {
                var extensionService = serviceScope.ServiceProvider.GetRequiredService<IExtensionService>();

                while (!await extensionService.CanAssign(taskId, firstOperatorId!))
                {
                    _logger.LogInformation($"На оператора {firstOperatorId} нельзя назначить задачу {taskId}.");

                    List<Channel<TaskFromChannel>>? channels = null;
                    while (channels == null || channels.All(x => x.Reader.Completion.IsCompleted))
                    {
                        var previousOperatorId = firstOperatorId;
                        firstOperatorId = _queue.NextPeek(previousOperatorId);
                        if (firstOperatorId == null)
                        {
                            _logger.LogInformation($"Задача не назначилась, тк нет операторов стоящих после {previousOperatorId} или которые могут взять данную задачу. " +
                                                   $"Очередь - {JsonSerializer.Serialize(_queue.GetOperators())}");
                            return null;
                        }

                        channels = _channelService.GetChannels(firstOperatorId);
                        if (channels == null)
                        {
                            RemoveFromQueue(firstOperatorId);
                            _logger.LogInformation($"Так как данная задача не может назначиться на оператора {previousOperatorId}, " +
                                                   $"то она перешла к оператору {firstOperatorId} в очереди и так же была не назначена, " +
                                                   $"потому что у оператора {firstOperatorId} не оказалось каналов, которые нужны для назначения задачи.");
                            // Заменяем на старого оператора, тк текущего оператора удалили из очереди
                            firstOperatorId = previousOperatorId;
                        }
                        else
                        {
                            if (channels.All(x => x.Reader.Completion.IsCompleted))
                            {
                                Remove(firstOperatorId);
                                _logger.LogInformation($"Так как данная задача не может назначиться на оператора {previousOperatorId}, " +
                                                       $"то она перешла к оператору {firstOperatorId} и так же была не назначена, " +
                                                       $"потому что у оператора {firstOperatorId} завершены все каналы, которые нужны для назначения задачи.");
                                // Заменяем на старого оператора, тк текущего оператора удалили из очереди
                                firstOperatorId = previousOperatorId;
                            }
                        }
                    }
                }
            }

            return firstOperatorId;
        }

        /// <summary>
        ///     Получить Id первого оператора из очереди
        /// </summary>
        private string? GetFirstOperatorId()
        {
            List<Channel<TaskFromChannel>>? channels = null;

            while (channels == null || channels.All(x => x.Reader.Completion.IsCompleted))
            {
                var firstOperatorId = _queue.Peek();
                if (firstOperatorId == null)
                {
                    return null;
                }

                channels = _channelService.GetChannels(firstOperatorId);
                if (channels == null)
                {
                    RemoveFromQueue(firstOperatorId);
                    _logger.LogInformation($"У оператора {firstOperatorId} не оказалось каналов для назначения задачи.");
                }
                else
                {
                    if (channels.All(x => x.Reader.Completion.IsCompleted))
                    {
                        Remove(firstOperatorId);
                        _logger.LogInformation($"У оператора {firstOperatorId} завершены все каналы для назначения задачи.");
                    }
                    else
                    {
                        return firstOperatorId;
                    }
                }
            }

            return null;
        }

        private void RemoveFromQueue(string operatorId)
        {
            if (_queue.TryRemove(operatorId))
            {
                _logger.LogInformation($"Оператор {operatorId} удален из очереди");
            }
            else
            {
                _logger.LogInformation($"Оператора {operatorId} не было в очереди, но была попытка удаления его");
            }
        }

        public void Dispose()
        {
            _timerForAssignTasks.Dispose();
        }
    }
}