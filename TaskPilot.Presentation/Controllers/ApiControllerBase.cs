using Microsoft.AspNetCore.Mvc;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Presentation.Models;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation.Controllers
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
        /// Surfaces ALL errors in the response body when there are multiple.
        /// </summary>
        protected ILocalizationService Localizer =>
     HttpContext.RequestServices.GetRequiredService<ILocalizationService>();
        protected ActionResult HandleResult<T>(Result<T> result,string? messageKey=null)
        {
            if (result.IsSuccess)
            {
                var message = messageKey != null ? Localizer.GetString(messageKey) : null;
                return Ok(ApiResponse.Success(result.Value, message));
            }

            if (result.Errors.Count > 1)
                return MapErrors(result.Errors);

            return MapError(result.Error);
        }

        /// <summary>
        /// Maps a <see cref="Result{T}"/> to an HTTP 201 Created on success.
        /// </summary>
        protected ActionResult HandleCreated<T>(Result<T> result, string? messageKey = null)
        {
            if (result.IsSuccess)
            {
                var message = messageKey != null ? Localizer.GetString(messageKey) : Localizer.GetString("Success");
                return StatusCode(201, ApiResponse.Success(result.Value, message));
            }

            return MapError(result.Error);
        }

        // ──────────────────────── Result → HTTP (no data) ────────────────────────

        /// <summary>
        /// Maps a <see cref="Result"/> (no data) to an HTTP response.
        /// Success → 200 OK with a message.  Failure → appropriate error status.
        /// Surfaces ALL errors in the response body when there are multiple.
        /// </summary>
        protected ActionResult HandleResult(Result result, string? messageKey = null)
        {
            if (result.IsSuccess)
            {
                var message = messageKey != null ? Localizer.GetString(messageKey) : null;
                return Ok(ApiResponse.Success(message));
            }

            if (result.Errors.Count > 1)
                return MapErrors(result.Errors);

            return MapError(result.Error);
        }

        // ──────────────────────── Error → HTTP ────────────────────────

        /// <summary>
        /// Central mapping from <see cref="ErrorType"/> to HTTP status code.
        /// This is the ONLY place in the entire solution where this translation happens.
        /// </summary>
        private ActionResult MapError(Error error)
        {
            var description = !string.IsNullOrEmpty(error.Description)
                ? error.Description
                : Localizer.GetString(error.Code);
                
            var errorDetail = new ErrorDetail 
            { 
                Code = error.Code, 
                Description = description,
                Metadata = error.Metadata
            };
            
            var response = new ApiResponse
            {
                Succeeded = false,
                Message = description,
                Errors = [errorDetail]
            };

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

        /// <summary>
        /// Maps multiple errors to an HTTP response.
        /// Uses the <see cref="ErrorType"/> of the first error to determine the HTTP status code,
        /// and includes all error details in the response body.
        /// </summary>
        private ActionResult MapErrors(IReadOnlyList<Error> errors)
        {
            var errorDetails = errors.Select(e => {
                var desc = Localizer.GetString(e.Code);
                if (desc == e.Code && !string.IsNullOrEmpty(e.Description))
                {
                    desc = e.Description;
                }
                return new ErrorDetail
                {
                    Code = e.Code,
                    Description = desc,
                    Metadata = e.Metadata
                };
            });
            var response = ApiResponse.Fail(errorDetails);
            var primaryType = errors[0].Type;

            return primaryType switch
            {
                ErrorType.Validation   => BadRequest(response),
                ErrorType.NotFound     => NotFound(response),
                ErrorType.Conflict     => Conflict(response),
                ErrorType.Unauthorized => Unauthorized(response),
                ErrorType.Forbidden    => StatusCode(403, response),
                _                      => StatusCode(500, response)
            };
        }
    }
}
