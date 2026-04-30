using Microsoft.AspNetCore.Mvc;
using TaskPilot.Api.Responses;
using TaskPilot.Application.Common.Errors;
using TaskPilot.Application.Common.Results;

namespace TaskPilot.Api.Controllers
{
    /// <summary>
    /// Base controller that wraps every response in a consistent <see cref="ApiResponse"/> envelope.
    /// Maps domain <see cref="Result"/> / <see cref="Result{T}"/> to the appropriate HTTP status code
    /// while keeping the JSON payload structure identical across all endpoints.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiControllerBase : ControllerBase
    {
        // ──────────────────────── Result<T> → HTTP (with data) ────────────────────────

        /// <summary>
        /// Maps a <see cref="Result{T}"/> to an HTTP response.
        /// Success → 200 OK with data.  Failure → appropriate error status.
        /// </summary>
        protected ActionResult HandleResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
                return Ok(ApiResponse.Success(result.Value));

            return MapError(result.Error);
        }

        /// <summary>
        /// Maps a <see cref="Result{T}"/> to an HTTP 201 Created on success.
        /// </summary>
        protected ActionResult HandleCreated<T>(Result<T> result, string? message = null)
        {
            if (result.IsSuccess)
                return StatusCode(201, ApiResponse.Success(result.Value, message ?? "Resource created successfully."));

            return MapError(result.Error);
        }

        // ──────────────────────── Result → HTTP (no data) ────────────────────────

        /// <summary>
        /// Maps a <see cref="Result"/> (no data) to an HTTP response.
        /// Success → 200 OK with a message.  Failure → appropriate error status.
        /// </summary>
        protected ActionResult HandleResult(Result result, string? message = null)
        {
            if (result.IsSuccess)
                return Ok(ApiResponse.Success(message));

            return MapError(result.Error);
        }

        // ──────────────────────── Error → HTTP ────────────────────────

        /// <summary>
        /// Central mapping from <see cref="ErrorType"/> to HTTP status code.
        /// This is the ONLY place in the entire solution where this translation happens.
        /// </summary>
        private ActionResult MapError(Error error)
        {
            var response = ApiResponse.Fail(error.Code, error.Description);

            return error.Type switch
            {
                ErrorType.Validation   => BadRequest(response),
                ErrorType.NotFound     => NotFound(response),
                ErrorType.Conflict     => Conflict(response),
                ErrorType.Unauthorized => Unauthorized(response),
                ErrorType.Forbidden    => StatusCode(403, response),
                _                      => StatusCode(500, response)   // ErrorType.Failure
            };
        }
    }
}
