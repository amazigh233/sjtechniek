using SjTechniek.Services;

namespace SjTechniek.Middleware;

public class VisitorTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private const string CookieName = "sj_vid";

    public VisitorTrackingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, VisitorTrackingService tracker)
    {
        var path = context.Request.Path.Value ?? "/";
        var isPageRequest = !path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
                         && !path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
                         && !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains('.')
                         && context.Request.Method == "GET";

        if (isPageRequest)
        {
            if (!context.Request.Cookies.TryGetValue(CookieName, out var sessionId)
                || string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
                context.Response.Cookies.Append(CookieName, sessionId, new CookieOptions
                {
                    MaxAge = TimeSpan.FromDays(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            tracker.TrackVisit(sessionId, path, ip);
        }

        await _next(context);
    }
}
