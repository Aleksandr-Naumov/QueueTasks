﻿namespace QueueTasks.Abstractions
{
    using System.Threading.Tasks;

    public interface ITasksManager
    {
        /// <summary>
        ///     Отправить задачу первому подходящему оператору из очереди
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        Task AddTask(string taskId);

        /// <summary>
        ///     Отправить задачу определенному оператору из очереди (задача заранее назначена на этого оператора)
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        Task AddTask(string taskId, string operatorId);
    }
}
