using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ShowDirector
{
    /// <summary>
    /// Adds a "Director" line to library cards in Jellyfin Web by injecting
    /// a small JavaScript file into index.html and serving that file (plus
    /// an admin config page) as embedded plugin resources.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Static accessor so ServerEntryPoint / other classes can reach the
        /// current configuration without re-resolving DI.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        public override string Name => "Show Director";

        public override string Description =>
            "Shows the director's name under the year on movie/series cards in Jellyfin Web.";

        // Generated once for this plugin - do not change, or existing
        // installs will be treated as a different plugin on upgrade.
        public override Guid Id => Guid.Parse("d1a9e9d2-6b2e-4d9a-8c39-2f6a1a9c4b2e");

        /// <summary>
        /// The URL path (relative to the Jellyfin base URL) that the injected
        /// script tag points to. This is served automatically by Jellyfin's
        /// plugin-page mechanism because we register it in GetPages() below.
        /// </summary>
        public const string JsPageName = "ShowDirector.js";

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "ShowDirectorConfig",
                    EmbeddedResourcePath = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}.Configuration.configPage.html",
                        GetType().Namespace)
                },
                new PluginPageInfo
                {
                    Name = JsPageName,
                    EmbeddedResourcePath = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}.Web.show-director.js",
                        GetType().Namespace)
                }
            };
        }
    }
}
