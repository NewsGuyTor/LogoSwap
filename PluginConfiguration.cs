using MediaBrowser.Model.Plugins;

namespace LogoSwap;

/// <summary>
/// Plugin configuration for LogoSwap.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        LogoPath = string.Empty;
    }

    /// <summary>
    /// Gets or sets the path to the custom logo file.
    /// </summary>
    public string LogoPath { get; set; }
}
