using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using eShop.Ordering.API.Utilities;

namespace eShop.Ordering.API.Middleware;

public class UserTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public UserTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var activity = Activity.Current;
        if (activity != null && context.User.Identity?.IsAuthenticated == true)
        {
            // Get user identifiers from claims
            var userId = context.User.FindFirst("sub")?.Value;
            var userName = context.User.Identity.Name;

            // Mask user information for privacy using the shared utility
            if (!string.IsNullOrEmpty(userId))
            {
                activity.SetTag("user_id", UserDataMasker.MaskUserId(userId));
            }

            if (!string.IsNullOrEmpty(userName))
            {
                activity.SetTag("user_name", UserDataMasker.MaskUserName(userName));
            }
        }

        await _next(context);
    }
}

// Extension method used to add the middleware to the HTTP request pipeline
public static class UserTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseUserTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<UserTrackingMiddleware>();
    }
}
