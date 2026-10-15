using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogoSwap;

/// <summary>
/// Reports plugin status and provides the client branding resources.
/// </summary>
public class LogoInjector : IHostedService
{
    private readonly ILogger<LogoInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoInjector"/> class.
    /// </summary>
    /// <param name="logger">The plugin logger.</param>
    public LogoInjector(ILogger<LogoInjector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogoSwap: Started successfully");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Gets optional JavaScript for clients using a script injector.
    /// </summary>
    /// <param name="logoUrl">The logo URL, including the server's base path.</param>
    /// <returns>The injection script.</returns>
    public static string GetInjectionScript(string logoUrl) => ReadResource("branding.js", logoUrl);

    /// <summary>
    /// Gets CSS for Jellyfin's supported custom branding setting.
    /// </summary>
    /// <param name="logoUrl">The logo URL, including the server's base path.</param>
    /// <returns>The branding stylesheet.</returns>
    public static string GetStylesheet(string logoUrl) => ReadResource("branding.css", logoUrl);

    private static string ReadResource(string name, string logoUrl)
    {
        using var stream = typeof(LogoInjector).Assembly.GetManifestResourceStream("LogoSwap.Views." + name)
            ?? throw new FileNotFoundException("Branding resource not found.", name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("__LOGO_URL__", JsonSerializer.Serialize(logoUrl));
    }
}
