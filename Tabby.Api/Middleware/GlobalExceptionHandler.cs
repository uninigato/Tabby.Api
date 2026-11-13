using Microsoft.AspNetCore.Diagnostics;

namespace Tabby.Api.Middleware
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            // The full details go to the log (Dozzle will show these later)
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

            // Same { message } shape the controllers already use for errors
            await httpContext.Response.WriteAsJsonAsync(
                new { message = "Something went wrong. Please try again." },
                cancellationToken);

            return true;
        }
    }
}
