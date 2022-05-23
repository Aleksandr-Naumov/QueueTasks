namespace QueueTasks.Contracts
{
    using System.Threading.Tasks;

    /// <summary>
    ///     Интерфейс для работы с текущим пользователем
    /// </summary>
    public interface ICurrentOperatorProvider
    {
        /// <summary>
        ///     Метод для получения Id текущего оператора
        /// </summary>
        /// <returns>Id оператора</returns>
        Task<string> GetCurrentOperatorId();
    }
}
