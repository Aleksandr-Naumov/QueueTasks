namespace QueueTasks.Services
{
    using System.Threading.Tasks;

    using Abstractions;

    internal class TasksManager : ITasksManager
    {
        private readonly IQueueOperatorManager _queueOperatorManager;
        public TasksManager(IQueueOperatorManager queueOperatorManager) =>
            _queueOperatorManager = queueOperatorManager;

        public async Task AddTask(string taskId) =>
            await _queueOperatorManager.AddNotAssignedTask(taskId);

        public async Task AddTask(string taskId, string operatorId) =>
            await _queueOperatorManager.AddAssignedTask(taskId, operatorId);
    }
}
