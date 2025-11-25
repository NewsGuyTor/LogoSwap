using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogoSwap;

/// <summary>
/// Hosted service that runs on startup to log plugin status.
/// The actual logo injection is handled client-side via JavaScript in the branding settings.
/// </summary>
public class LogoInjector : IHostedService
{
    private readonly ILogger<LogoInjector> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoInjector"/> class.
    /// </summary>
    /// <param name="logger">Instance of the <see cref="ILogger{LogoInjector}"/> interface.</param>
    public LogoInjector(ILogger<LogoInjector> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogoSwap: Started successfully");
        
        var logoPath = Plugin.Instance?.Configuration.LogoPath;
        if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
        {
            _logger.LogInformation("LogoSwap: Custom logo configured at {Path}", logoPath);
        }
        else
        {
            _logger.LogInformation("LogoSwap: No custom logo configured. Upload one via the plugin settings.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the JavaScript code that should be added to Jellyfin's branding settings.
    /// </summary>
    /// <returns>The JavaScript injection code.</returns>
    public static string GetInjectionScript()
    {
        return @"/* LogoSwap */
(function() {
    'use strict';
    
    var customLogoUrl = '/logoswap/image?v=' + new Date().getTime();
    
    function replaceLogos() {
        // Target elements that use background-image for logos
        // Main header logo - h3.pageTitle.pageTitleWithLogo
        document.querySelectorAll('.pageTitleWithLogo, .pageTitleWithDefaultLogo').forEach(function(el) {
            el.style.backgroundImage = 'url(' + customLogoUrl + ')';
            el.style.backgroundSize = 'contain';
            el.style.backgroundRepeat = 'no-repeat';
        });
        
        // Sidebar/drawer logo
        document.querySelectorAll('.mainDrawer .logoImage, .navDrawerLogo, .adminDrawerLogo').forEach(function(el) {
            el.style.backgroundImage = 'url(' + customLogoUrl + ')';
            el.style.backgroundSize = 'contain';
            el.style.backgroundRepeat = 'no-repeat';
            el.style.backgroundPosition = 'center';
        });
        
        // Handle any img elements with logo/banner in src
        document.querySelectorAll('img').forEach(function(img) {
            var src = (img.src || '').toLowerCase();
            if ((src.includes('banner-') || src.includes('icon-transparent')) && !src.includes('/logoswap/')) {
                img.src = customLogoUrl;
            }
        });
        
        // imgLogoIcon class
        document.querySelectorAll('.imgLogoIcon').forEach(function(el) {
            if (el.tagName === 'IMG') {
                el.src = customLogoUrl;
            }
        });
    }
    
    // Initial run
    replaceLogos();
    
    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', replaceLogos);
    }
    
    // Run on Jellyfin navigation events
    ['viewshow', 'pageshow', 'viewbeforeshow'].forEach(function(evt) {
        document.addEventListener(evt, function() {
            setTimeout(replaceLogos, 50);
            setTimeout(replaceLogos, 200);
            setTimeout(replaceLogos, 500);
        });
    });
    
    // Observe DOM changes
    var observer = new MutationObserver(function() {
        setTimeout(replaceLogos, 100);
    });
    
    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
    } else {
        document.addEventListener('DOMContentLoaded', function() {
            observer.observe(document.body, { childList: true, subtree: true });
        });
    }
    
    // Periodic fallback
    setInterval(replaceLogos, 2000);
})();
/* END LogoSwap */";
    }
}
