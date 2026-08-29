using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Features.Security;
using App.GitHealth.Api.Tests.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace App.GitHealth.Api.Tests.Security;

public sealed class LocalRequestSecurityTests(ApiApplicationFactory factory)
    : IClassFixture<ApiApplicationFactory>
{
    [Fact]
    public async Task HtmlNavigationCreatesRandomStrictCookiesAndSecurityHeaders()
    {
        using var firstClient = factory.CreateRawClient();
        using var secondClient = factory.CreateRawClient();

        using var first = await NavigateAsync(firstClient);
        using var second = await NavigateAsync(secondClient);
        var firstCookies = ReadCookies(first);
        var secondCookies = ReadCookies(second);

        AssertStrictCookie(firstCookies, LocalSession.SessionCookieName, isHttpOnly: true);
        AssertStrictCookie(firstCookies, LocalSession.AntiforgeryCookieName, isHttpOnly: true);
        AssertStrictCookie(firstCookies, LocalSession.RequestTokenCookieName, isHttpOnly: false);
        Assert.NotEqual(
            CookieValue(firstCookies, LocalSession.SessionCookieName),
            CookieValue(secondCookies, LocalSession.SessionCookieName));
        AssertSecurityHeaders(first);
    }

    [Fact]
    public async Task HealthEndpointRemainsPublicWithoutCreatingSession()
    {
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://example.com");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
        AssertSecurityHeaders(response);
    }

    [Fact]
    public async Task DevelopmentBootstrapCreatesSessionForAngularProxy()
    {
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, LocalSessionEndpoints.Path);
        request.Headers.Add("Origin", "http://localhost:4200");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await client.SendAsync(request);
        var cookies = ReadCookies(response);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        AssertStrictCookie(cookies, LocalSession.SessionCookieName, isHttpOnly: true);
        AssertStrictCookie(cookies, LocalSession.AntiforgeryCookieName, isHttpOnly: true);
        AssertStrictCookie(cookies, LocalSession.RequestTokenCookieName, isHttpOnly: false);
    }

    [Fact]
    public async Task MutationWithoutNavigationReturnsProblemDetailsWithoutRedirect()
    {
        using var client = factory.CreateRawClient();

        using var response = await ValidateRepositoryAsync(client, configure: null);

        await AssertSecurityProblemAsync(
            response,
            "security.invalid_antiforgery_token");
    }

    [Fact]
    public async Task MutationWithInvalidTokenReturnsProblemDetails()
    {
        using var client = factory.CreateRawClient();
        using var navigation = await NavigateAsync(client);
        var cookies = ReadCookies(navigation);

        using var response = await ValidateRepositoryAsync(client, request =>
        {
            AddCookies(request, cookies);
            request.Headers.Add(LocalSession.AntiforgeryHeaderName, "invalid");
        });

        await AssertSecurityProblemAsync(
            response,
            "security.invalid_antiforgery_token");
    }

    [Fact]
    public async Task ValidSessionAndTokenReachApiEndpoint()
    {
        using var client = factory.CreateRawClient();
        using var navigation = await NavigateAsync(client);
        var cookies = ReadCookies(navigation);

        using var response = await ValidateRepositoryAsync(client, request =>
        {
            AddCookies(request, cookies);
            request.Headers.Add(
                LocalSession.AntiforgeryHeaderName,
                CookieValue(cookies, LocalSession.RequestTokenCookieName));
        });
        var code = await ReadProblemCodeAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("repository.invalid_path", code);
    }

    [Theory]
    [InlineData("Origin", "https://example.com")]
    [InlineData("Origin", "http://localhost:4201")]
    [InlineData("Sec-Fetch-Site", "cross-site")]
    [InlineData("Sec-Fetch-Site", "same-site")]
    public async Task CrossSiteApiRequestReturnsProblemDetails(string name, string value)
    {
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime");
        request.Headers.TryAddWithoutValidation(name, value);

        using var response = await client.SendAsync(request);

        await AssertSecurityProblemAsync(response, "security.cross_site_request");
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://localhost:4200")]
    public async Task ConfiguredLocalOriginIsAccepted(string origin)
    {
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Sec-Fetch-Site", "same-origin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }

    [Fact]
    public async Task NonLoopbackHostIsRejected()
    {
        using var client = factory.CreateRawClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime");
        request.Headers.Host = "githealth.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task OpenApiIsUnavailableOutsideDevelopment()
    {
        using var productionFactory = new ApiApplicationFactory();
        using var configuredFactory = productionFactory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Production));
        using var client = configuredFactory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("endpoint.not_found", await ReadProblemCodeAsync(response));
    }

    private static async Task<HttpResponseMessage> NavigateAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ValidateRepositoryAsync(
        HttpClient client,
        Action<HttpRequestMessage>? configure)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/projects/validate")
        {
            Content = JsonContent.Create(new { path = "" }),
        };
        configure?.Invoke(request);
        return await client.SendAsync(request);
    }

    private static string[] ReadCookies(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").ToArray();

    private static void AddCookies(HttpRequestMessage request, string[] cookies)
    {
        var pairs = cookies.Select(cookie => cookie.Split(';', 2)[0]);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", pairs));
    }

    private static string CookieValue(string[] cookies, string name)
    {
        var pair = cookies
            .Select(cookie => cookie.Split(';', 2)[0])
            .Single(cookie => cookie.StartsWith($"{name}=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(pair.Split('=', 2)[1]);
    }

    private static void AssertStrictCookie(
        string[] cookies,
        string name,
        bool isHttpOnly)
    {
        var cookie = cookies.Single(value => value.StartsWith(
            $"{name}=",
            StringComparison.Ordinal));
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            isHttpOnly,
            cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains(
            "frame-ancestors 'none'",
            response.Headers.GetValues("Content-Security-Policy").Single());

        // Angular déclare <base href="/"> : 'none' rendrait toute adresse profonde illisible.
        Assert.Contains(
            "base-uri 'self'",
            response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains(
            "geolocation=()",
            response.Headers.GetValues("Permissions-Policy").Single());
    }

    private static async Task AssertSecurityProblemAsync(
        HttpResponseMessage response,
        string expectedCode)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedCode, await ReadProblemCodeAsync(response));
        Assert.Null(response.Headers.Location);
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("code").GetString();
    }
}
