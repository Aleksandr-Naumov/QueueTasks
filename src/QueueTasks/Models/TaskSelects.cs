﻿namespace QueueTasks.Models
{
    using System;

    internal class TaskSelects
    {
        /// <summary>
        ///     Id оператора
        /// </summary>
        public string OperatorId { get; set; } = default!;

        /// <summary>
        ///     Время когда задачу выдали оператору
        /// </summary>
        public DateTime DateTime { get; set; }
    }
}
