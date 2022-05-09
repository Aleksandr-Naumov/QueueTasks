namespace QueueTasks.Models
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using Abstractions;

    using Timer = System.Timers.Timer;

    public class TimerTasks
    {
        private readonly ConcurrentDictionary<string, TaskSelects> _tasks = new ConcurrentDictionary<string, TaskSelects>();
        private readonly IQueueOperatorManager _queueOperatorManager;

        public TimerTasks(IQueueOperatorManager queueOperatorManager)
        {
            _queueOperatorManager = queueOperatorManager;
        }

        /// <summary>
        ///     Добавить потенциальную задачу для оператора, чтобы потом отправить ее на другогоу оператора,
        ///     если оператор вышел со страницы выбора взятия в работу задачи
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        public async Task Add(string taskId, string operatorId)
        {
            _tasks.TryAdd(taskId, new TaskSelects() { DateTime = DateTime.UtcNow, OperatorId = operatorId });
            await StartTimer();
        }

        /// <summary>
        ///     Проверяет оператора, что данная задача назначена на него
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="operatorId"></param>
        /// <returns></returns>
        public bool CheckOperator(string taskId, string operatorId) =>
            _tasks.ContainsKey(taskId) && _tasks[taskId].OperatorId == operatorId;

        private static readonly SemaphoreSlim Pool = new SemaphoreSlim(1, 1);
        private static readonly Timer Timer = new Timer() { Interval = 40 * 1000 };

        /// <summary>
        ///     Удаление задачи из очереди ожидания взятия
        /// </summary>
        /// <param name="taskId"></param>
        public async Task Remove(string taskId)
        {
            await Pool.WaitAsync();

            _tasks.TryRemove(taskId, out _);
            TryStopTimer();

            Pool.Release();
        }

        private static readonly TimeSpan Difference = new TimeSpan(0, 0, 40);

        private async Task StartTimer()
        {
            await Pool.WaitAsync();

            if (!Timer.Enabled)
            {
                Timer.Elapsed +=
                    async (sender, e) =>
                    {
                        foreach (var task in _tasks)
                        {
                            var gds = DateTime.UtcNow - task.Value.DateTime;
                            if (DateTime.UtcNow - task.Value.DateTime > Difference)
                            {
                                _queueOperatorManager.Remove(task.Value.OperatorId);
                                _tasks.TryRemove(task.Key, out _);
                                await _queueOperatorManager.AddNotAssignedTask(task.Key);
                            }
                        }

                        TryStopTimer();
                    };
                Timer.AutoReset = true;
                Timer.Enabled = true;
                Timer.Start();
            }

            Pool.Release();
        }

        private void TryStopTimer()
        {
            if (Timer.Enabled && _tasks.IsEmpty)
            {
                Timer.Stop();
            }
        }
    }
}
