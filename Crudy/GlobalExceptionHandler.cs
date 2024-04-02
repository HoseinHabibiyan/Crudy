using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Raven.Client.Exceptions;

namespace Crudy
{
    internal sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var problemDetails = new ProblemDetails
            {
               Detail = exception.Message
            };

            switch (exception)
            {
                case UnauthorizedAccessException:
                    problemDetails.Title = "Unauthorized";
                    problemDetails.Status = StatusCodes.Status401Unauthorized;
                    break;
                case BadHttpRequestException:
                    problemDetails.Title = "Bad Request";
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    break;
                default:
                    problemDetails.Title = "Internal server error";
                    problemDetails.Status = StatusCodes.Status500InternalServerError;
                    break;
            }

            _logger.LogError(
                exception, "Exception occurred: {Message}", exception.Message);

            httpContext.Response.StatusCode = problemDetails.Status.Value;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
