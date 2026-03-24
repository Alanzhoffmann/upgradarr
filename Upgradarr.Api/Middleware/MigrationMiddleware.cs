using Upgradarr.Data.Interfaces;

namespace Upgradarr.Api.Middleware;

public class MigrationMiddleware
{
    private readonly RequestDelegate _next;

    public MigrationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMigrationState migrationState)
    {
        if (!migrationState.IsDone)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { Message = "Service Unavailable: Database migrations are currently running." });
            return;
        }
        await _next(context);
    }
}
