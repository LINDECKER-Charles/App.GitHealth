using App.GitHealth.Core.Projects;

namespace App.GitHealth.Core.Tests.Projects;

public sealed class ProjectTests
{
    [Fact]
    public void CreateBuildsAProjectWithValidatedDefaults()
    {
        var project = Project.Create(" GitHealth ", "D:/repositories/githealth");

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("GitHealth", project.DisplayName);
        Assert.Equal("refs/heads/*", project.Settings.BranchNamespace);
    }

    [Fact]
    public void RestoreRejectsAnEmptyIdentifier()
    {
        Assert.Throws<ArgumentException>(() =>
            Project.Restore(Guid.Empty, "GitHealth", "D:/repositories/githealth"));
    }
}
