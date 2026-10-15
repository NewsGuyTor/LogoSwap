using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace LogoSwap;

/// <summary>
/// Replaces the web client's favicon without modifying Jellyfin's files or requiring JavaScript.
/// </summary>
public sealed class FaviconStartupFilter : IStartupFilter
{
    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<FaviconMiddleware>();
            next(app);
        };
    }
}

/// <summary>
/// Serves the configured favicon for Jellyfin's plain and fingerprinted favicon URLs.
/// </summary>
public sealed class FaviconMiddleware
{
    private static readonly Regex FaviconUrl = new(@"/web/favicon(?:[.-][a-zA-Z0-9_-]+)?\.(?:ico|png|svg)$", RegexOptions.CultureInvariant);
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaviconMiddleware"/> class.
    /// </summary>
    /// <param name="next">The remaining request pipeline.</param>
    public FaviconMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Serves a custom icon or leaves the default response untouched.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>The asynchronous request operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = Plugin.Instance?.Configuration.FaviconPath;
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && FaviconUrl.IsMatch(context.Request.Path.Value ?? string.Empty)
            && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            SetHeaders(context.Response, path);
            context.Response.ContentLength = file.Length;
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await file.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
            }

            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    internal static void SetHeaders(HttpResponse response, string path)
    {
        response.ContentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".svg" => "image/svg+xml",
            ".ico" => "image/x-icon",
            _ => "image/png"
        };
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        // SVG stays inert even when someone opens the public image URL as a document.
        response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
    }
}
