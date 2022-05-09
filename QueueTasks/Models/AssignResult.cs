namespace QueueTasks.Models
{
    public class AssignResult
    {
        /// <summary>
        ///     Произошло ли азначение успешно
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Текст описания из-за чего назначение задачи на данного оператора не возможно
        /// </summary>
        public string Error { get; set; } = default!;
    }
}
