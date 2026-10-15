using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LogoSwap.Controllers;

/// <summary>
/// Manages a browser icon independently of the full-size logo.
/// </summary>
[ApiController]
[Route("logoswap/favicon")]
public class FaviconController : ControllerBase
{
    private const int MaximumBytes = 2 * 1024 * 1024;
    private readonly IApplicationPaths _paths;

    /// <summary>
    /// Initializes a new instance of the <see cref="FaviconController"/> class.
    /// </summary>
    /// <param name="paths">Jellyfin's application paths.</param>
    public FaviconController(IApplicationPaths paths) => _paths = paths;

    /// <summary>
    /// Uploads a PNG, ICO, or SVG favicon. It takes effect on the next page load.
    /// </summary>
    /// <param name="file">The icon file.</param>
    /// <returns>The upload result.</returns>
    [HttpPost]
    [Authorize(Policy = Policies.RequiresElevation)]
    [RequestSizeLimit(MaximumBytes + 64 * 1024)]
    public async Task<ActionResult> Upload([Required] IFormFile file)
    {
        if (file.Length == 0 || file.Length > MaximumBytes)
        {
            return BadRequest("Choose an icon smaller than 2 MB.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var contents = new MemoryStream();
        await file.CopyToAsync(contents, HttpContext.RequestAborted).ConfigureAwait(false);
        var bytes = contents.ToArray();
        if (!IsValidIcon(bytes, extension))
        {
            return BadRequest("Choose a valid PNG, ICO, or SVG icon.");
        }

        var plugin = Plugin.Instance;
        if (plugin == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "LogoSwap is not ready.");
        }

        var directory = Path.Combine(_paths.PluginConfigurationsPath, "LogoSwap");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "favicon" + extension);
        var temporary = Path.Combine(directory, Guid.NewGuid() + ".tmp");
        try
        {
            await System.IO.File.WriteAllBytesAsync(temporary, bytes, HttpContext.RequestAborted).ConfigureAwait(false);
            System.IO.File.Move(temporary, destination, overwrite: true);
            plugin.Configuration.FaviconPath = destination;
            plugin.SaveConfiguration();
        }
        finally
        {
            System.IO.File.Delete(temporary);
        }

        return Ok("Favicon uploaded. Reload the page to see it.");
    }

    /// <summary>
    /// Gets the uploaded favicon for previews or separately hosted web clients.
    /// </summary>
    /// <returns>The icon file.</returns>
    [HttpGet]
    public ActionResult Get()
    {
        var path = Plugin.Instance?.Configuration.FaviconPath;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound("No custom favicon configured.");
        }

        FaviconMiddleware.SetHeaders(Response, path);
        return File(System.IO.File.OpenRead(path), Response.ContentType!);
    }

    /// <summary>
    /// Restores Jellyfin's default favicon without changing the logo.
    /// </summary>
    /// <returns>The deletion result.</returns>
    [HttpDelete]
    [Authorize(Policy = Policies.RequiresElevation)]
    public ActionResult Delete()
    {
        var plugin = Plugin.Instance;
        var path = plugin?.Configuration.FaviconPath;
        if (string.IsNullOrEmpty(path))
        {
            return NotFound("No custom favicon configured.");
        }

        System.IO.File.Delete(path);
        plugin!.Configuration.FaviconPath = string.Empty;
        plugin.SaveConfiguration();
        return Ok("Default favicon restored. Reload the page to see it.");
    }

    private static bool IsValidIcon(byte[] contents, string extension)
    {
        if (extension == ".png")
        {
            return contents.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        }

        if (extension == ".ico")
        {
            return contents.Length >= 6 && contents[0] == 0 && contents[1] == 0
                && contents[2] == 1 && contents[3] == 0 && (contents[4] != 0 || contents[5] != 0);
        }

        if (extension != ".svg")
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(contents);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader).Root?.Name == XName.Get("svg", "http://www.w3.org/2000/svg");
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
