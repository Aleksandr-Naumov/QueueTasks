namespace QueueTasks.Services
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    using Models;

    public class ChannelService
    {
        private readonly ConcurrentDictionary<string, List<Channel<TaskFromChannel>>> _channels = new ConcurrentDictionary<string, List<Channel<TaskFromChannel>>>();

        /// <summary>
        ///     Создать канал для того, чтобы уведомлять о новой задаче оператора, который встал в очередь.
        ///     Или добавить в список каналов новый канал, если оператор отправил запрос на повторное ожидание
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>Канал</returns>
        public Channel<TaskFromChannel> CreateChannel(string operatorId)
        {
            var channel = Channel.CreateBounded<TaskFromChannel>(
                new BoundedChannelOptions(1) { SingleReader = true, SingleWriter = true });

            _channels.AddOrUpdate(operatorId,
                (key) => new List<Channel<TaskFromChannel>>()
                {
                    channel
                },
                (key, value) =>
                {
                    var newValue = new List<Channel<TaskFromChannel>>(value)
                    {
                        channel
                    };
                    return newValue;
                });

            return channel;
        }

        /// <summary>
        ///     Уведомить оператора о появлении задачи, если он стоит в очереди.
        /// </summary>
        /// <param name="taskId">Id задачи, которую выдали оператору для взятия в работу</param>
        /// <param name="operatorId">Id оператора, которому выдали задачу</param>
        /// <param name="assigned">Заявка уже назначена на оператора или нет</param>
        public async Task WriteToChannel(string taskId, string operatorId, bool assigned)
        {
            if (!_channels.TryGetValue(operatorId, out var channels))
            {
                return;
            }

            if (channels.All(x => x.Reader.Completion.IsCompleted))
            {
                Remove(operatorId);
                return;
            }

            foreach (var channel in channels)
            {
                if (!channel.Reader.Completion.IsCompleted)
                {
                    try
                    {
                        await channel.Writer.WriteAsync(new TaskFromChannel(taskId, assigned));
                    }
                    catch (ChannelClosedException)
                    {
                        // Канал могли успеть закрыть, поэтому просто обрабатываем это, а таймер через время перекинет на другого оператора из очереди задачу
                    }
                }
            }
        }

        /// <summary>
        ///     Получить каналы оператора
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>Каналы оператора, если он находится в очереди; иначе null</returns>
        public List<Channel<TaskFromChannel>>? GetChannels(string operatorId)
        {
            _channels.TryGetValue(operatorId, out var channels);
            return channels;
        }

        /// <summary>
        ///     Удаляет каналы оператора
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        public void Remove(string operatorId) => _channels.TryRemove(operatorId, out _);
    }
}
