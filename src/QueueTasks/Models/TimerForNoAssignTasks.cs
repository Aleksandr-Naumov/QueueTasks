namespace QueueTasks.Models
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    using Services;

    using Timer = System.Timers.Timer;

    /// <summary>
    ///     Таймер для не назначенных задач
    /// </summary>
    internal class TimerForNoAssignTasks : IDisposable
    {
        private readonly ConcurrentDictionary<string, object?> _tasks = new ConcurrentDictionary<string, object?>();
        private readonly QueueOperatorManager _queueOperatorManager;

        public TimerForNoAssignTasks(QueueOperatorManager queueOperatorManager) => _queueOperatorManager = queueOperatorManager;

        /// <summary>
        ///     Добавить задачу, чтобы потом отправить ее оператору,
        ///     если очередь из операторов не пустая и не было потенциальных операторов на взятие этой задачи.
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        public async Task Add(string taskId)
        {
            if (!_queueOperatorManager.IsEmpty())
            {
                _tasks.TryAdd(taskId, null);
                await TryStartTimer();
            }
            else
            {
                await TryStopTimer();
            }
        }

        private readonly SemaphoreSlim _pool = new SemaphoreSlim(1, 1);
        private readonly Timer _timer = new Timer() { Interval = 10 * 1000 }; //TODO: сделать изменяемым значение

        private async Task TryStartTimer()
        {
            await _pool.WaitAsync();

            try
            {
                if (!_timer.Enabled)
                {
                    // не добавляем повторно метод
                    _timer.Elapsed -= RetryAssign;
                    _timer.Elapsed += RetryAssign;

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

        private async void RetryAssign(object e, EventArgs a)
        {
            if (_queueOperatorManager.IsEmpty())
            {
                _tasks.Clear();
                await TryStopTimer();
            }
            else
            {
                foreach (var task in _tasks)
                {
                    _tasks.TryRemove(task.Key, out _);
                    await _queueOperatorManager.AddTask(task.Key);

                }
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
            _pool.Dispose();
        }
    }
}