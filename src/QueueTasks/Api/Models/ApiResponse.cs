namespace QueueTasks.Api.Models
{
    internal class ApiResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; } = default!;
        public int ErrorCode { get; set; }
        public object Result { get; set; } = default!;

        public static ApiResponse CreateFailure(string error = null!, int? errorCode = null, object result = default!) =>
            new ApiResponse
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode ?? 0,
                Result = result
            };

        public static ApiResponse CreateSuccess(object result = default!) =>
            new ApiResponse()
            {
                Success = true,
                Result = result
            };
    }
}
