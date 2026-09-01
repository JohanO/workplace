namespace Workplace.ApiService.WorkCalendar;

public class SyncKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string HeaderName = "X-Sync-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        var expectedKey = configuration["OutlookSync:SyncKey"];

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            expectedKey is null ||
            providedKey != expectedKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}
