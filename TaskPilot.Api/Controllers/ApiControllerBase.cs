using Microsoft.AspNetCore.Mvc;
using TaskPilot.Application.Common.Errors;
using TaskPilot.Application.Common.Results;

namespace TaskPilot.Api.Controllers
{
    /// <summary>
    /// Base controller that maps domain <see cref="Result"/> / <see cref="Result{T}"/>
    /// to proper HTTP responses.  This is the ONLY place where ErrorType → HTTP status
    /// code mapping lives.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>
        /// Maps a <see cref="Result{T}"/> to an HTTP response.
        /// On success returns 200 OK with the data payload.
        /// On failure maps <see cref="ErrorType"/> to the appropriate HTTP status code.
        /// </summary>
        protected ActionResult HandleResult<T>(Result<T> result)
        {
            if (result.IsSuccess)
                return Ok(result.Value);

            return HandleError(result.Error);
        }

        /// <summary>
        /// Maps a <see cref="Result"/> (no data) to an HTTP response.
        /// On success returns 204 No Content.
        /// </summary>
        protected ActionResult HandleResult(Result result)
        {
            if (result.IsSuccess)
                return NoContent();

            return HandleError(result.Error);
        }

        /// <summary>
        /// Maps an <see cref="Error"/> to the matching HTTP response.
        /// Central mapping — change here if you add new ErrorTypes.
        /// </summary>
        private ActionResult HandleError(Error error)
        {
            var body = new
            {
                error.Code,
                error.Description
            };

            return error.Type switch
            {
                ErrorType.Validation   => BadRequest(body),
                ErrorType.NotFound     => NotFound(body),
                ErrorType.Conflict     => Conflict(body),
                ErrorType.Unauthorized => Unauthorized(body),
                ErrorType.Forbidden    => StatusCode(403, body),
                _                      => StatusCode(500, body)   // ErrorType.Failure
            };
        }
    }
}
