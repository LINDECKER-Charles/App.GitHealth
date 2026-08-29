using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.GitHealth.Api.Features.Runtime;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Runtime;

public sealed class RuntimeEndpointTests
{
    [Fact]
    public async Task RuntimeAllowsDirectoryBrowsingInNativeMode()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/runtime");

        Assert.Equal(JsonValueKind.Null, payload.GetProperty("repositoriesRoot").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("initialRepositoryPath").ValueKind);
        Assert.True(payload.GetProperty("canBrowseDirectories").GetBoolean());
        Assert.Equal("native", payload.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task RuntimeExposesTheRepositoryProvidedByTheLauncher()
    {
        var repositoryPath = Path.Combine(Path.GetTempPath(), "githealth-initial-repository");
        using var factory = new ApiApplicationFactory
        {
            InitialRepositoryPath = repositoryPath,
        };
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/runtime");

        Assert.Equal(
            repositoryPath,
            payload.GetProperty("initialRepositoryPath").GetString());
    }

    [Fact]
    public async Task RuntimeExposesConfiguredRootInDockerMode()
    {
        const string repositoriesRoot = "/repositories";
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repositoriesRoot,
        };
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/runtime");

        Assert.Equal(repositoriesRoot, payload.GetProperty("repositoriesRoot").GetString());
        Assert.False(payload.GetProperty("canBrowseDirectories").GetBoolean());
        Assert.Equal("docker", payload.GetProperty("mode").GetString());
    }

    [Fact]
    public async Task DirectoriesListAccessibleSubdirectoriesInStableOrder()
    {
        using var directory = TemporaryDirectory.Create();
        directory.AddDirectory("zeta");
        directory.AddDirectory("alpha");
        directory.AddDirectory("beta");
        directory.AddFile("ignored.txt");
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await GetDirectoriesAsync(client, directory.Path);

        Assert.Equal(directory.Path, payload.GetProperty("currentPath").GetString());
        Assert.Equal(
            Directory.GetParent(directory.Path)?.FullName,
            payload.GetProperty("parentPath").GetString());
        Assert.Equal(
            ["alpha", "beta", "zeta"],
            DirectoryNames(payload));
        var firstDirectory = payload.GetProperty("directories")[0];
        Assert.Equal(
            Path.Combine(directory.Path, "alpha"),
            firstDirectory.GetProperty("path").GetString());
        Assert.False(payload.GetProperty("isTruncated").GetBoolean());
    }

    [Fact]
    public async Task DirectoriesUseUserProfileWhenPathIsOmitted()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>(
            "/api/runtime/directories");

        var expected = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        expected = string.IsNullOrWhiteSpace(expected) ? Environment.CurrentDirectory : expected;
        Assert.Equal(Path.GetFullPath(expected), payload.GetProperty("currentPath").GetString());
    }

    [Fact]
    public async Task DirectoriesReturnNullParentAtFileSystemRoot()
    {
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
        Assert.False(string.IsNullOrWhiteSpace(root));

        var payload = await GetDirectoriesAsync(client, root);

        Assert.Equal(
            new DirectoryInfo(root).FullName,
            payload.GetProperty("currentPath").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("parentPath").ValueKind);
    }

    [Fact]
    public async Task DirectoriesAreLimitedToMaximumResultCount()
    {
        using var directory = TemporaryDirectory.Create();
        var requestedCount = DirectoryBrowser.MaximumDirectoryCount + 1;
        for (var index = 0; index < requestedCount; index++)
        {
            directory.AddDirectory($"directory-{index:D3}");
        }

        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        var payload = await GetDirectoriesAsync(client, directory.Path);

        Assert.Equal(
            DirectoryBrowser.MaximumDirectoryCount,
            payload.GetProperty("directories").GetArrayLength());
        Assert.True(payload.GetProperty("isTruncated").GetBoolean());
        var names = DirectoryNames(payload);
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
    }

    [Fact]
    public async Task DirectoriesRejectMissingPathWithoutTechnicalDetails()
    {
        using var directory = TemporaryDirectory.Create();
        using var factory = new ApiApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(DirectoryUrl(
            Path.Combine(directory.Path, "missing")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("runtime.directory_not_found", problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.False(problem.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task DirectoriesAreHiddenInDockerMode()
    {
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = "/repositories",
        };
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/runtime/directories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            "runtime.directory_browsing_unavailable",
            problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }

    private static async Task<JsonElement> GetDirectoriesAsync(
        HttpClient client,
        string? path) => await client.GetFromJsonAsync<JsonElement>(DirectoryUrl(path));

    private static string DirectoryUrl(string? path) =>
        $"/api/runtime/directories?path={Uri.EscapeDataString(path ?? string.Empty)}";

    private static string[] DirectoryNames(JsonElement payload) => payload
        .GetProperty("directories")
        .EnumerateArray()
        .Select(directory => directory.GetProperty("name").GetString()!)
        .ToArray();

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GitHealth-directory-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void AddDirectory(string name) => Directory.CreateDirectory(
            System.IO.Path.Combine(Path, name));

        public void AddFile(string name) => File.WriteAllText(
            System.IO.Path.Combine(Path, name),
            "test");

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
