using Jellyfin.Plugin.ShowDirector.Middleware;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ShowDirector
{
    /// <summary>
    /// Registers the HTML-rewriting middleware and its startup filter with
    /// Jellyfin's dependency injection container. Jellyfin discovers any
    /// class implementing IPluginServiceRegistrator in a loaded plugin
    /// assembly and calls RegisterServices automatically at startup - no
    /// further wiring is needed.
    /// </summary>
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddTransient<IndexHtmlInjectionMiddleware>();
            serviceCollection.AddSingleton<IStartupFilter, ShowDirectorStartupFilter>();
        }
    }
}

