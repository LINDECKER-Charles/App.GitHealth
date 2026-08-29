using App.GitHealth.Benchmarks.Fixtures;

namespace App.GitHealth.Git.IntegrationTests.Benchmark;

public sealed class GitCommandExecutorTests
{
    [Fact]
    public void BenchmarkEnvironmentRemovesHostGitOverrides()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["GIT_CONFIG_GLOBAL"] = "hostile-config",
            ["GIT_DIR"] = "hostile-metadata",
            ["GIT_OBJECT_DIRECTORY"] = "hostile-objects",
            ["GIT_TRACE2_EVENT"] = "hostile-trace.json",
            ["GIT_WORK_TREE"] = "hostile-worktree",
            ["UNRELATED"] = "preserved",
        };

        GitCommandExecutor.ConfigureEnvironment(environment);

        Assert.Equal(OperatingSystem.IsWindows() ? "NUL" : "/dev/null",
            environment["GIT_CONFIG_GLOBAL"]);
        Assert.Equal("1", environment["GIT_CONFIG_NOSYSTEM"]);
        Assert.DoesNotContain("GIT_DIR", environment.Keys);
        Assert.DoesNotContain("GIT_OBJECT_DIRECTORY", environment.Keys);
        Assert.DoesNotContain("GIT_TRACE2_EVENT", environment.Keys);
        Assert.DoesNotContain("GIT_WORK_TREE", environment.Keys);
        Assert.Equal("0", environment["GIT_OPTIONAL_LOCKS"]);
        Assert.Equal("preserved", environment["UNRELATED"]);
    }
}
