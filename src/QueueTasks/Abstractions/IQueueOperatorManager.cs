namespace QueueTasks.Abstractions
{
    using System.Collections.Generic;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Models;

    public interface IQueueOperatorManager
    {
        /// <summary>
        ///     Добавляет в очередь оператора и возвращает канал
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>Канал для отмены или получения новой задачи</returns>
        Channel<TaskFromChannel> AddToQueue(string operatorId);

        /// <summary>
        ///     Удаление оператора из очереди и его каналов, если эти каналы уже завершены
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        void Remove(string operatorId);

        /// <summary>
        ///     Удаление оператора из очереди и всех его каналов сразу
        /// </summary>
        /// <param name="operatorId">Id оператора</param>
        void RemoveAll(string operatorId);

        /// <summary>
        ///     Проверяет пустая очереди или нет
        /// </summary>
        /// <returns>true, если операторов нет в очереди; иначе false</returns>
        bool IsEmpty();

        /// <summary>
        ///     Проверяет что потенциальная задача была выдана оператору
        /// </summary>
        /// <param name="taskId">Id задачи</param>
        /// <param name="operatorId">Id оператора</param>
        /// <returns>true, если задача была выдана оператору; иначе false</returns>
        Task<bool> IsTaskForOperator(string taskId, string operatorId);

        /// <summary>
        ///     Получить список операторов в очереди с информацией о приоритете и статусе
        /// </summary>
        /// <returns>Список операторов</returns>
        IEnumerable<OperatorDto> GetOperators();
    }
}
