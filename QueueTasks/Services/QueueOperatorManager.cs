namespace QueueTasks.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using Abstractions;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Models;

    internal class QueueOperatorManager : IQueueOperatorManager, ITasksManager, IDisposable
    {
        private readonly ChannelService _channelService = new ChannelService();
        private readonly QueueOperators _queue = new QueueOperators();
        private readonly TimerTasks _timerTasks;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QueueOperatorManager> _logger;

        public QueueOperatorManager(
            IServiceProvider serviceProvider,
            ILogger<QueueOperatorManager> logger)
        {
            _timerTasks = new TimerTasks(this);
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task AddTask(string taskId)
        {
            var (firstOperatorId, channels) = GetFirstOperator();
            if (firstOperatorId == null)
            {
                return;
            }

            while (channels.All(x => x.Reader.Completion.IsCompleted))
            {
                Remove(firstOperatorId);

                (firstOperatorId, channels) = GetFirstOperator();
                if (firstOperatorId == null)
                {
                    return;
                }
            }

            using (var serviceScope = _serviceProvider.CreateScope())
            {
                var extensionService = serviceScope.ServiceProvider.GetRequiredService<IExtensionService>();

                while (!await extensionService.CanAssign(taskId, firstOperatorId))
                {
                    firstOperatorId = _queue.NextPeek(firstOperatorId);
                    if (firstOperatorId == null)
                    {
                        return;
                    }
                }
            }

            await _timerTasks.Add(taskId, firstOperatorId);

            _queue.ChangeStatusToSelects(firstOperatorId);
            await _channelService.WriteToChannel(taskId, firstOperatorId, assigned: false);
        }

        public async Task AddTask(string taskId, string operatorId)
        {
            await _channelService.WriteToChannel(taskId, operatorId, assigned: true);
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

        public void Remove(string operatorId)
        {
            var channels = _channelService.GetChannels(operatorId);
            if (channels != null && channels!.All(x => x.Reader.Completion.IsCompleted))
            {
                RemoveFromQueue(operatorId);
                _channelService.Remove(operatorId);
            }
        }

        public bool IsEmpty() => _queue.IsEmpty();

        public IEnumerable<OperatorDto> GetOperators() => _queue.GetOperators();

        public async Task<bool> IsTaskForOperator(string taskId, string operatorId)
        {
            if (_timerTasks.CheckOperator(taskId, operatorId))
            {
                await _timerTasks.Remove(taskId);
                return true;
            }
            return false;
        }

        private (string? firstOperatorId, List<Channel<TaskFromChannel>> channels) GetFirstOperator()
        {
            var firstOperatorId = _queue.Peek();
            if (firstOperatorId == null)
            {
                return (null, null!);
            }

            var channels = _channelService.GetChannels(firstOperatorId);
            if (channels == null)
            {
                // эт не норм, оператор есть в очереди, а каналов у оператора нет
                RemoveFromQueue(firstOperatorId);
                return (null, null!);
            }

            return (firstOperatorId, channels);
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
            _timerTasks.Dispose();
        }
    }
}
