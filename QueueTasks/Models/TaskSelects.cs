namespace QueueTasks.Models
{
    using System;

    public class TaskSelects
    {
        /// <summary>
        ///     Id оператора
        /// </summary>
        public string OperatorId { get; set; } = default!;

        /// <summary>
        ///     Время когда задачу выдали данному оператору
        /// </summary>
        public DateTime DateTime { get; set; }
    }
}
