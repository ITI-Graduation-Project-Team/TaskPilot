using System.Text.Json.Serialization;

namespace TaskPilot.Presentation.Models
{
    /// <summary>
    /// A consistent JSON envelope returned by every API endpoint.
    /// Guarantees the same shape regardless of success or failure.
    ///
    /// Success example:
    /// {
    ///   "succeeded": true,
    ///   "message": null,
    ///   "errors": null,
    ///   "data": { ... }
    /// }
    ///
    /// Failure example:
    /// {
    ///   "succeeded": false,
    ///   "message": "Project was not found.",
    ///   "errors": [{ "code": "NOT_FOUND", "description": "Project was not found." }],
    ///   "data": null
    /// }
    /// </summary>
    public class ApiResponse
    {
        public bool Succeeded { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ErrorDetail>? Errors { get; init; }

        // ──────────────────────── Factory Methods ────────────────────────

        public static ApiResponse Success(string? message = null)
            => new() { Succeeded = true, Message = message };

        public static ApiResponse<T> Success<T>(T data, string? message = null)
            => new() { Succeeded = true, Data = data, Message = message };

        public static ApiResponse Fail(string code, string description)
            => new()
            {
                Succeeded = false,
                Message = description,
                Errors = [new ErrorDetail { Code = code, Description = description }]
            };

        public static ApiResponse Fail(IEnumerable<ErrorDetail> errors)
        {
            var list = errors.ToList();
            return new()
            {
                Succeeded = false,
                Message = list.Count > 0 ? list[0].Description :null,
                Errors = list
            };
        }

        public static ApiResponse<T> Fail<T>(string code, string description)
            => new()
            {
                Succeeded = false,
                Message = description,
                Errors = [new ErrorDetail { Code = code, Description = description }]
            };

        public static ApiResponse<T> Fail<T>(IEnumerable<ErrorDetail> errors)
        {
            var list = errors.ToList();
            return new()
            {
                Succeeded = false,
                Message = list.Count > 0 ? list[0].Description : null,
                Errors = list
            };
        }

    }   // end ApiResponse

    /// <summary>
    /// Typed envelope that carries a data payload.
    /// </summary>
    public class ApiResponse<T> : ApiResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public T? Data { get; init; }
    }

    /// <summary>
    /// Describes a single error inside the Errors array.
    /// </summary>
    public class ErrorDetail
    {
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
