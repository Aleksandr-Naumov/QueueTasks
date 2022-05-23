namespace QueueTasks.Models
{
    using Newtonsoft.Json;

    internal class TaskFromChannel
    {
        public TaskFromChannel(string taskId, bool assigned)
        {
            TaskId = taskId;
            Assigned = assigned;
        }

        /// <summary>
        ///     Id задачи
        /// </summary>
        [JsonProperty("taskId")]
        public string TaskId { get; private set; }

        /// <summary>
        ///     Пришла ли задача уже назначенной на оператора
        /// </summary>
        [JsonProperty("assigned")]
        public bool Assigned { get; private set; }
    }
}
