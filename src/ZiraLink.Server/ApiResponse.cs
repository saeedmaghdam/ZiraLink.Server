namespace ZiraLink.Server
{
    public class ApiResponse<T>
    {
        public T? Data { get; set; }
        public bool Status { get; set; }
        public string ErrorMessage { get; set; }
        public int ErrorCode { get; set; }

        public static ApiResponse<T> CreateSuccessResponse()
        {
            return new ApiResponse<T>
            {
                Data = default(T),
                Status = true,
                ErrorCode = 0,
                ErrorMessage = String.Empty
            };
        }

        public static ApiResponse<T> CreateSuccessResponse(T data)
        {
            return new ApiResponse<T>
            {
                Data = data,
                Status = true,
                ErrorCode = 0,
                ErrorMessage = String.Empty
            };
        }

        public static ApiResponse<T> CreateFailureResponse()
        {
            return new ApiResponse<T>
            {
                Data = default(T),
                Status = false,
                ErrorCode = 9000,
                ErrorMessage = String.Empty
            };
        }

        public static ApiResponse<T> CreateFailureResponse(string errorMessage)
        {
            return new ApiResponse<T>
            {
                Data = default(T),
                Status = false,
                ErrorCode = 9000,
                ErrorMessage = errorMessage
            };
        }

        public static ApiResponse<T> CreateFailureResponse(int errorCode)
        {
            return new ApiResponse<T>
            {
                Data = default(T),
                Status = false,
                ErrorCode = errorCode,
                ErrorMessage = String.Empty
            };
        }

        public static ApiResponse<T> CreateFailureResponse(string errorMessage, int errorCode)
        {
            return new ApiResponse<T>
            {
                Data = default(T),
                Status = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
