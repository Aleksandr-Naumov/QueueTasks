namespace QueueTasks.Abstractions
{
    using System.Threading.Tasks;

    using Models;

    /// <summary>
    ///     Интерфейс для расширения
    /// </summary>
    public interface IExtensionService
    {
        /// <summary>
        ///     Метод получения свободной задачи и назначения на оператора, если такая имеется.
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>Id задачи, если есть свободные; иначе null</returns>
        Task<string?> GetFreeTaskId(string operatorId);

        /// <summary>
        ///     Метод для проверки возможности назначения задачи на оператора.
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>true, если можно назначить; иначе false</returns>
        Task<bool> CanAssign(string taskId, string operatorId);

        /// <summary>
        ///     Метод для назначения потенциальной задачи на оператора.
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>
        ///     Результат назначения: 
        ///     true, если задача успешно назначена на оператора;
        ///     иначе false вместе с текстом почему назначение не возможно на этого оператора
        /// </returns>
        Task<AssignResult> TryAssignTask(string taskId, string operatorId);
    }
}
