namespace QueueTasks.Models
{
    public class OperatorDto
    {
        public string OperatorId { get; set; } = default!;

        public long Priority { get; set; }

        public string Status { get; set; } = default!;
    }
}
