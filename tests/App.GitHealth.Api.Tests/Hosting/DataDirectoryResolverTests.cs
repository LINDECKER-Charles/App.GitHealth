using App.GitHealth.Api.Hosting;

namespace App.GitHealth.Api.Tests.Hosting;

public sealed class DataDirectoryResolverTests
{
    [Fact]
    public void ResolveUsesConfiguredDirectoryBeforePlatformDefault()
    {
        var environment = CreateEnvironment(RuntimePlatform.Linux);
        var configured = Path.Combine(Path.GetTempPath(), "githealth-custom-data");
        var resolver = new DataDirectoryResolver(environment);

        var result = resolver.Resolve(configured);

        Assert.Equal(Path.GetFullPath(configured), result);
    }

    [Fact]
    public void ResolveMakesConfiguredRelativeDirectoryAbsolute()
    {
        var environment = CreateEnvironment(RuntimePlatform.Linux);
        var resolver = new DataDirectoryResolver(environment);

        var result = resolver.Resolve("custom-data");

        Assert.Equal(
            Path.GetFullPath("custom-data", environment.CurrentDirectory),
            result);
    }

    [Fact]
    public void ResolveUsesLocalApplicationDataOnWindows()
    {
        var environment = CreateEnvironment(RuntimePlatform.Windows) with
        {
            LocalApplicationDataPath = "C:\\Users\\Ada\\AppData\\Local",
        };

        var result = new DataDirectoryResolver(environment).Resolve(null);

        Assert.Equal("C:\\Users\\Ada\\AppData\\Local\\GitHealth", result);
    }

    [Fact]
    public void ResolveFallsBackToUserProfileOnWindows()
    {
        var environment = CreateEnvironment(RuntimePlatform.Windows) with
        {
            LocalApplicationDataPath = string.Empty,
        };

        var result = new DataDirectoryResolver(environment).Resolve(null);

        Assert.Equal("C:\\Users\\Ada\\AppData\\Local\\GitHealth", result);
    }

    [Fact]
    public void ResolveUsesApplicationSupportOnMacOS()
    {
        var environment = CreateEnvironment(RuntimePlatform.MacOS);

        var result = new DataDirectoryResolver(environment).Resolve(null);

        Assert.Equal("/Users/ada/Library/Application Support/GitHealth", result);
    }

    [Fact]
    public void ResolveUsesAbsoluteXdgDataHomeOnLinux()
    {
        var environment = CreateEnvironment(RuntimePlatform.Linux) with
        {
            XdgDataHomePath = "/srv/ada/data",
        };

        var result = new DataDirectoryResolver(environment).Resolve(null);

        Assert.Equal("/srv/ada/data/GitHealth", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/data")]
    public void ResolveFallsBackToLocalShareWhenXdgDataHomeIsInvalid(string? xdgDataHome)
    {
        var environment = CreateEnvironment(RuntimePlatform.Linux) with
        {
            XdgDataHomePath = xdgDataHome,
        };

        var result = new DataDirectoryResolver(environment).Resolve(null);

        Assert.Equal("/home/ada/.local/share/GitHealth", result);
    }

    private static LauncherEnvironment CreateEnvironment(RuntimePlatform platform) => new()
    {
        Platform = platform,
        CurrentDirectory = Path.GetFullPath(Path.GetTempPath()),
        UserProfilePath = platform switch
        {
            RuntimePlatform.Windows => "C:\\Users\\Ada",
            RuntimePlatform.MacOS => "/Users/ada",
            _ => "/home/ada",
        },
        LocalApplicationDataPath = "C:\\Users\\Ada\\AppData\\Local",
    };
}
