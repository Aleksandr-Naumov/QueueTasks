namespace QueueTasks.Api.Models
{
    internal class ApiResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; } = default!;
        public int ErrorCode { get; set; }

        public static ApiResponse CreateFailure(string error = null!, int? errorCode = null) =>
            new ApiResponse
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode ?? 0
            };

        public static ApiResponse CreateSuccess() =>
            new ApiResponse()
            {
                Success = true
            };
    }
}
