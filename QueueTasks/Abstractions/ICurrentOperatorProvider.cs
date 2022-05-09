namespace QueueTasks.Abstractions
{
    using System.Threading.Tasks;

    public interface ICurrentOperatorProvider
    {
        /// <summary>
        ///     Метод для получения Id текущего оператора
        /// </summary>
        /// <returns>Id оператора</returns>
        Task<string> GetCurrentOperatorId();
    }
}
