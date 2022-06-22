namespace QueueTasks.Api.Models
{
    public class ApiResponse
    {
        /// <summary>
        ///     Успешно ли завершен запрос
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Текст ошибки
        /// </summary>
        public string Error { get; set; } = default!;

        /// <summary>
        ///     Код ошибки
        /// </summary>
        public int ErrorCode { get; set; }

        /// <summary>
        ///     Данные представляющие результат выполненного успешно запроса
        /// </summary>
        public object? Result { get; set; } = default!;

        public static ApiResponse CreateFailure(string error = null!, int? errorCode = null, object? result = default) =>
            new ApiResponse
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode ?? 0,
                Result = result
            };

        public static ApiResponse CreateSuccess(object? result = default) =>
            new ApiResponse()
            {
                Success = true,
                Result = result
            };
    }
}
