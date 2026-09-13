using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ShowDirector
{
    /// <summary>
    /// Configuration for the Show Director plugin, editable from the
    /// plugin's page in the Jellyfin admin dashboard.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            Enabled = true;
            MultiDirectorSeparator = ", ";
            MaxDirectors = 2;
            ApplyToMovies = true;
            ApplyToSeries = true;
            ApplyToEpisodes = false;
        }

        /// <summary>
        /// Master on/off switch. Turning this off removes the injected
        /// script tag on next server start (and the JS also checks this
        /// flag at runtime so a live client can be toggled without a restart).
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// String used to join multiple directors, e.g. ", " or " / ".
        /// </summary>
        public string MultiDirectorSeparator { get; set; }

        /// <summary>
        /// Cap on how many director names to show before truncating with "et al."
        /// </summary>
        public int MaxDirectors { get; set; }

        public bool ApplyToMovies { get; set; }

        public bool ApplyToSeries { get; set; }

        public bool ApplyToEpisodes { get; set; }
    }
}
