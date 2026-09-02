using App.GitHealth.Core.Branches;
using App.GitHealth.Core.Projects;

namespace App.GitHealth.Core.Tests.Projects;

public sealed class ProjectSettingsTests
{
    private const string LocalMain = "refs/heads/main";
    private const string RemoteMain = "refs/remotes/origin/main";

    [Fact]
    public void BaselinesKeepTheDeclaredOrderAndTheFirstOneIsTheReference()
    {
        var settings = new ProjectSettings
        {
            Baselines = [new GitRef(RemoteMain), new GitRef(LocalMain)],
        };

        Assert.Equal(
            [RemoteMain, LocalMain],
            settings.Baselines.Select(baseline => baseline.FullName));
        Assert.Equal(settings.Baselines[0], settings.Reference);
    }

    [Fact]
    public void AProjectWithoutBaselineHasNoReference()
    {
        var settings = new ProjectSettings { Baselines = [] };

        Assert.Empty(settings.Baselines);
        Assert.Null(settings.Reference);
        Assert.Null(ProjectSettings.Default.Reference);
    }

    [Fact]
    public void ABaselineListedTwiceIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new ProjectSettings
        {
            Baselines = [new GitRef(LocalMain), new GitRef(RemoteMain), new GitRef(LocalMain)],
        });
    }

    [Fact]
    public void MoreBaselinesThanTheMaximumAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new ProjectSettings
        {
            Baselines = CreateBaselines(ProjectSettings.MaximumBaselineCount + 1),
        });
    }

    [Fact]
    public void ExactlyTheMaximumNumberOfBaselinesIsAccepted()
    {
        var settings = new ProjectSettings
        {
            Baselines = CreateBaselines(ProjectSettings.MaximumBaselineCount),
        };

        Assert.Equal(ProjectSettings.MaximumBaselineCount, settings.Baselines.Count);
        Assert.Equal("refs/heads/baseline-a", settings.Reference!.FullName);
    }

    [Fact]
    public void ReplacingTheBaselinesLeavesTheRestOfThePolicyUntouched()
    {
        var settings = new ProjectSettings
        {
            Baselines = [new GitRef(LocalMain)],
            BranchNamespace = "refs/remotes/origin/*",
            Thresholds = ActivityThresholds.Create(7, 45),
            Policy = BranchPolicy.Create(["refs/heads/tmp/*"], []),
        };

        var reordered = settings with { Baselines = [new GitRef(RemoteMain)] };

        Assert.Equal(RemoteMain, reordered.Reference!.FullName);
        Assert.Equal("refs/remotes/origin/*", reordered.BranchNamespace);
        Assert.Equal(7, reordered.Thresholds.ActiveUntilDays);
        Assert.Equal(["refs/heads/tmp/*"], reordered.Policy.ExcludedPatterns);
    }

    private static GitRef[] CreateBaselines(int count) => Enumerable
        .Range(0, count)
        .Select(index => new GitRef("refs/heads/baseline-" + (char)('a' + index)))
        .ToArray();
}
