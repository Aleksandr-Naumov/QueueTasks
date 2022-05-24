namespace QueueTasks.Exceptions
{
    using System;

    internal class PossibilityWaitTasksException : Exception
    {
        public PossibilityWaitTasksException() : base("Нет возможности встать в очередь и ожидать задачи") { }
    }
}
