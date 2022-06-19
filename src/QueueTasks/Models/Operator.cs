namespace QueueTasks.Models
{
    using Enums;
    using System;

    internal class Operator
    {
        public Operator()
        {
            Time = DateTime.Now;
            Status = OperatorStatus.Free;
        }

        /// <summary>
        ///     Время встатия оператора в очередь
        /// </summary>
        public DateTime Time { get; private set; }

        /// <summary>
        ///     Статус оператора в очереди
        /// </summary>
        public OperatorStatus Status { get; private set; }

        /// <summary>
        ///     Сменить статус у оператора, если потенциальную задачу уже назначали на другого оператора,
        ///     чтобы ему досталась следующая новая задача (оператор также остался первым в очереди)
        /// </summary>
        public void ChangeStatusToFree() => Status = OperatorStatus.Free;

        /// <summary>
        ///     Сменить статус у оператора, если ему дали потенциальную задачу,
        ///     чтобы ему не приходили новые задачи (когда он думает брать задачу или нет)
        /// </summary>
        public void ChangeStatusToSelects() => Status = OperatorStatus.Selects;
    }
}