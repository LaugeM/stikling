using Microsoft.Extensions.Options;

namespace Stikling.Api.Sharing;

public static class ShareHost
{
    /// <summary>
    /// On the share host (share.stikling.app), the address of a page is just <c>/{token}</c>. This
    /// moves it under <c>/s</c> where the pages are, and sends the front door to the app. It does
    /// nothing on any other host, or when no share host is set, as locally, where <c>/s/...</c> is
    /// used as it is. Call it before routing.
    /// </summary>
    public static IApplicationBuilder UseShareHost(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<ShareOptions>>().Value;
            if (options.IsShareHost(context.Request.Host.Host))
            {
                var path = context.Request.Path;
                if (!path.HasValue || path.Value == "/")
                {
                    context.Response.Redirect(ShareStyles.AppUrl);
                    return;
                }

                context.Request.Path = new PathString(SharePageEndpoints.Prefix).Add(path);
            }

            await next(context);
        });
}
