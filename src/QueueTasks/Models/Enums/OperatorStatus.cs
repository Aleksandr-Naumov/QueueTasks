namespace QueueTasks.Models.Enums
{
    using System.ComponentModel;

    internal enum OperatorStatus
    {
        [Description("Свободный")]
        Free = 1,

        [Description("Выбирает брать задачу или нет")]
        Selects = 2
    }
}
