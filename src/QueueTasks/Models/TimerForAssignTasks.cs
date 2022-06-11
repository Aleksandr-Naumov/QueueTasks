namespace QueueTasks.Models
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using Services;

    using Timer = System.Timers.Timer;

    /// <summary>
    ///     Таймер для назначенных задач
    /// </summary>
    internal class TimerForAssignTasks : IDisposable
    {
        private readonly ConcurrentDictionary<string, TaskSelects> _tasks = new ConcurrentDictionary<string, TaskSelects>();
        private readonly QueueOperatorManager _queueOperatorManager;

        public TimerForAssignTasks(QueueOperatorManager queueOperatorManager) => _queueOperatorManager = queueOperatorManager;

        /// <summary>
        ///     Добавить потенциальную задачу для оператора, чтобы потом отправить ее на другогоу оператора,
        ///     если оператор вышел со страницы выбора взятия в работу задачи
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        public async Task Add(string taskId, string operatorId)
        {
            _tasks.TryAdd(taskId, new TaskSelects() { DateTime = DateTime.UtcNow, OperatorId = operatorId });
            await TryStartTimer();
        }

        /// <summary>
        ///     Проверяет оператора, что данная задача назначена на него
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="operatorId"></param>
        /// <returns></returns>
        public bool CheckOperator(string taskId, string operatorId) =>
            _tasks.ContainsKey(taskId) && _tasks[taskId].OperatorId == operatorId;

        /// <summary>
        ///     Удаление задачи из очереди ожидания взятия
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        public async Task Remove(string taskId)
        {
            _tasks.TryRemove(taskId, out _);
            await TryStopTimer();
        }

        private readonly SemaphoreSlim _pool = new SemaphoreSlim(1, 1);
        private readonly Timer _timer = new Timer() { Interval = 10 * 1000 }; //TODO: сделать изменяемым значение
        private readonly TimeSpan _expirationThinkingTime = new TimeSpan(0, 0, 30); //TODO: сделать изменяемым значение

        private async Task TryStartTimer()
        {
            await _pool.WaitAsync();

            try
            {
                if (!_timer.Enabled)
                {
                    // не добавляем повторно метод
                    _timer.Elapsed -= CheckTasksForExpirationThinkingTime;
                    _timer.Elapsed += CheckTasksForExpirationThinkingTime;

                    _timer.AutoReset = true;
                    _timer.Enabled = true;
                    _timer.Start();
                }
            }
            finally
            {
                _pool.Release();
            }
        }

        private async Task TryStopTimer()
        {
            await _pool.WaitAsync();

            try
            {
                if (_timer.Enabled && _tasks.IsEmpty)
                {
                    _timer.Stop();
                }
            }
            finally
            {
                _pool.Release();
            }
        }

        private async void CheckTasksForExpirationThinkingTime(object e, EventArgs a)
        {
            foreach (var task in _tasks)
            {
                if (DateTime.UtcNow - task.Value.DateTime > _expirationThinkingTime)
                {
                    _queueOperatorManager.Remove(task.Value.OperatorId);
                    _tasks.TryRemove(task.Key, out _);
                    await _queueOperatorManager.AddTask(task.Key);
                }
            }
            await TryStopTimer();
        }

        public void Dispose()
        {
            _timer.Dispose();
            _pool.Dispose();
        }
    }
}
