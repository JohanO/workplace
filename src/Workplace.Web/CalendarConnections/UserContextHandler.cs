using Microsoft.Identity.Web;

namespace Workplace.Web.CalendarConnections;

public class UserContextHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var objectId = httpContextAccessor.HttpContext?.User?.GetObjectId();
        if (!string.IsNullOrEmpty(objectId))
        {
            request.Headers.Add("X-Workplace-User", objectId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
