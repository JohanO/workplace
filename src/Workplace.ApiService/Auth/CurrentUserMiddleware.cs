namespace Workplace.ApiService.Auth;

public class CurrentUserMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Workplace-User";

    public async Task InvokeAsync(HttpContext context, CurrentUser currentUser)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        currentUser.UserId = userId.ToString();
        await next(context);
    }
}
