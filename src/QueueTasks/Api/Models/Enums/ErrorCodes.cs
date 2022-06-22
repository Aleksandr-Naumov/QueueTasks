namespace QueueTasks.Api.Models.Enums
{
    using System.ComponentModel;

    internal enum ErrorCodes
    {
        [Description("Задача уже назначена")]
        TaskAlreadyAssigned = 910
    }
}
