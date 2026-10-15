using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using LogoSwap.Controllers;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LogoSwap.Tests;

public sealed class BrandingTests : IDisposable
{
    private const string Svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 16 16\"><path fill=\"red\" d=\"M0 0h16v16H0z\"/></svg>";
    private readonly string _directory = Directory.CreateTempSubdirectory("logoswap-test-").FullName;
    private readonly Mock<IApplicationPaths> _paths = new();

    public BrandingTests()
    {
        _paths.SetupGet(paths => paths.PluginsPath).Returns(_directory);
        _paths.SetupGet(paths => paths.PluginConfigurationsPath).Returns(_directory);
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(s => s.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>())).Returns(new PluginConfiguration());
        _ = new Plugin(_paths.Object, serializer.Object);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/jellyfin")]
    [InlineData("/media/jellyfin")]
    public async Task AssetsAndGeneratedBrandingPreserveBasePath(string basePath)
    {
        using var server = CreateServer(basePath);
        using var client = server.CreateClient();
        var logoPath = Path.Combine(_directory, "logo.png");
        await File.WriteAllTextAsync(logoPath, "logo");
        Plugin.Instance!.Configuration.LogoPath = logoPath;
        var status = await client.GetFromJsonAsync<JsonElement>(basePath + "/logoswap/status");
        Assert.Equal(basePath + "/logoswap/image", status.GetProperty("logoUrl").GetString());
        foreach (var resource in new[] { "styles", "script" })
        {
            var text = await client.GetStringAsync(basePath + "/logoswap/" + resource);
            Assert.Contains('"' + basePath + "/logoswap/image?v=", text);
            Assert.DoesNotContain("__LOGO_URL__", text);
            Assert.Equal(text, await client.GetStringAsync(basePath + "/logoswap/" + resource));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("/jellyfin")]
    public async Task FaviconUploadServesFingerprintedWebIconAndDeleteRestoresDefault(string basePath)
    {
        using var server = CreateServer(basePath);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        using var form = Icon("icon.svg", Svg);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync(basePath + "/logoswap/favicon", form)).StatusCode);
        var status = await client.GetFromJsonAsync<JsonElement>(basePath + "/logoswap/status");
        Assert.True(status.GetProperty("hasFavicon").GetBoolean());
        Assert.Equal(basePath + "/logoswap/favicon", status.GetProperty("faviconUrl").GetString());
        foreach (var url in new[] { "/logoswap/favicon", "/web/favicon.ico", "/web/favicon.bc8d51405ec040305a87.ico" })
        {
            using var icon = await client.GetAsync(basePath + url);
            Assert.Equal("image/svg+xml", icon.Content.Headers.ContentType!.MediaType);
            Assert.Equal(Svg, await icon.Content.ReadAsStringAsync());
            Assert.Contains("sandbox", icon.Headers.GetValues("Content-Security-Policy").Single());
            Assert.True(icon.Headers.CacheControl!.NoStore);
        }

        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, basePath + "/web/favicon.ico"));
        Assert.Equal(Svg.Length, head.Content.Headers.ContentLength);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
        Assert.Equal("default icon", await client.GetStringAsync(basePath + "/web/other.png"));
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync(basePath + "/logoswap/favicon")).StatusCode);
        Assert.Equal("default icon", await client.GetStringAsync(basePath + "/web/favicon.bc8d51405ec040305a87.ico"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(basePath + "/logoswap/favicon")).StatusCode);
    }

    [Theory]
    [InlineData("icon.svg", "<html><script>alert(1)</script></html>")]
    [InlineData("icon.svg", "<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg'>&x;</svg>")]
    [InlineData("icon.png", "not a PNG")]
    [InlineData("icon.ico", "not an ICO")]
    [InlineData("icon.html", Svg)]
    public async Task InvalidIconKeepsExistingFavicon(string filename, string contents)
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        using var valid = Icon("icon.svg", Svg);
        (await client.PostAsync("/logoswap/favicon", valid)).EnsureSuccessStatusCode();
        using var invalid = Icon(filename, contents);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/logoswap/favicon", invalid)).StatusCode);
        Assert.Equal(Svg, await client.GetStringAsync("/logoswap/favicon"));
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("ico", "image/x-icon")]
    public async Task RasterFaviconsRoundTripWithoutChangingLogo(string extension, string contentType)
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j3f8AAAAASUVORK5CYII=");
        var bytes = png;
        if (extension == "ico")
        {
            // ICO directory with one embedded PNG image.
            bytes = new byte[22 + png.Length];
            bytes[2] = 1;
            bytes[4] = 1;
            bytes[6] = 1;
            bytes[7] = 1;
            bytes[10] = 1;
            bytes[12] = 32;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), png.Length);
            bytes[18] = 22;
            png.CopyTo(bytes, 22);
        }

        var logoPath = Path.Combine(_directory, "logo.png");
        await File.WriteAllBytesAsync(logoPath, png);
        Plugin.Instance!.Configuration.LogoPath = logoPath;
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "favicon." + extension);
        (await client.PostAsync("/logoswap/favicon", form)).EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/web/favicon.ico");
        Assert.Equal(contentType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        (await client.DeleteAsync("/logoswap/delete")).EnsureSuccessStatusCode();
        Assert.Equal(bytes, await client.GetByteArrayAsync("/logoswap/favicon"));
        Assert.False(File.Exists(logoPath));
    }

    [Fact]
    public async Task OversizedFaviconIsRejected()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Role", "admin");
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[2 * 1024 * 1024 + 1]), "file", "icon.png");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/logoswap/favicon", form)).StatusCode);
        Assert.Equal(string.Empty, Plugin.Instance!.Configuration.FaviconPath);
    }

    [Theory]
    [InlineData("/logoswap/favicon", "POST")]
    [InlineData("/logoswap/favicon", "DELETE")]
    [InlineData("/logoswap/upload", "POST")]
    [InlineData("/logoswap/delete", "DELETE")]
    public async Task BrandingWritesRequireAdministrator(string url, string method)
    {
        using var server = CreateServer();
        using var client = server.CreateClient();
        using var anonymous = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "user");
        using var ordinaryUser = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.Equal(HttpStatusCode.Forbidden, ordinaryUser.StatusCode);
    }

    private TestServer CreateServer(string basePath = "")
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_paths.Object);
                services.AddTransient<IStartupFilter, FaviconStartupFilter>();
                services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
                services.AddAuthorization(options => options.AddPolicy(Policies.RequiresElevation, policy => policy.RequireRole("admin")));
                services.AddControllers().AddApplicationPart(typeof(LogoController).Assembly);
            })
            .Configure(app =>
            {
                if (basePath.Length > 0) app.UsePathBase(basePath);
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
                app.Run(context => context.Response.WriteAsync("default icon"));
            }));
    }

    private static MultipartFormDataContent Icon(string filename, string contents)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(contents)), "file", filename);
        return form;
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

internal sealed class TestAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role.ToString()) }, "test");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "test")));
    }
}
