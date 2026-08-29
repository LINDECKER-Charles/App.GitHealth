using App.GitHealth.Core.Projects;

namespace App.GitHealth.Core.Tests.Projects;

public sealed class ProjectOrganizationTests
{
    [Fact]
    public void ANewProjectIsNeitherFavoriteNorGrouped()
    {
        var project = Project.Create("GitHealth", "D:/repositories/githealth");

        Assert.False(project.Organization.IsFavorite);
        Assert.Null(project.Organization.GroupName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyGroupNameMeansNoGroup(string? candidate)
    {
        var organization = new ProjectOrganization { GroupName = candidate };

        Assert.Null(organization.GroupName);
    }

    [Fact]
    public void AGroupNameIsTrimmed()
    {
        var organization = new ProjectOrganization { GroupName = "  Back-office  " };

        Assert.Equal("Back-office", organization.GroupName);
    }

    [Fact]
    public void AGroupNameHasAnExplicitSizeLimit()
    {
        var tooLong = new string('a', ProjectOrganization.MaximumGroupNameLength + 1);

        Assert.Throws<ArgumentException>(() => new ProjectOrganization { GroupName = tooLong });
    }
}
