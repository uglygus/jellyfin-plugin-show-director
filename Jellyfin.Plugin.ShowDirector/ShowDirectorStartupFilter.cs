using System;
using Jellyfin.Plugin.ShowDirector.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.ShowDirector
{
    /// <summary>
    /// Adds <see cref="IndexHtmlInjectionMiddleware"/> to the ASP.NET Core
    /// request pipeline early, so it sits in front of the static file
    /// middleware that actually serves index.html and can rewrite its
    /// response before it goes out over the wire.
    /// </summary>
    public class ShowDirectorStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.UseMiddleware<IndexHtmlInjectionMiddleware>();
                next(app);
            };
        }
    }
}
