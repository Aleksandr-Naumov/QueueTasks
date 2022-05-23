namespace QueueTasks.Models
{
    internal class ApiResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; } = default!;
        public int ErrorCode { get; set; }

        public static ApiResponse CreateFailure(string error, int errorCode)
        {
            return new ApiResponse { Success = false, Error = error, ErrorCode = errorCode };
        }
    }
}
