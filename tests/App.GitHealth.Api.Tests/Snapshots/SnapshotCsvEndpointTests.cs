using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using App.GitHealth.Api.Features.Snapshots;
using App.GitHealth.Api.Tests.Hosting;

namespace App.GitHealth.Api.Tests.Snapshots;

public sealed class SnapshotCsvEndpointTests
{
    private const string Filters =
        "topology=Ahead&recommendation=Excluded&isExcluded=true"
        + "&sort=name&direction=desc";

    [Fact]
    public async Task CsvUsesTheSameFiltersAndOrderAsLatestJson()
    {
        using var repository = GitTestRepository.Create(aheadBranchCount: 2);
        repository.AddAheadBranchWithAuthor("feature/formula", "=SUM(Zoë,2)");
        repository.AddAheadBranchWithAuthor("feature/café", "Zoë Martin");
        using var factory = new ApiApplicationFactory
        {
            RepositoriesRoot = repository.RootPath,
        };
        using var client = factory.CreateClient();
        var projectId = await ApiTestWorkflow.CreateProjectAsync(
            client,
            repository.RepositoryPath);
        await ApiTestWorkflow.AnalyzeAsync(client, projectId);
        await ExcludeFeatureBranchesAsync(client, projectId);

        var json = await client.GetFromJsonAsync<JsonElement>(
            $"/api/projects/{projectId}/analyses/latest/branches?{Filters}&pageSize=200");
        var expectedReferences = json.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("referenceName").GetString())
            .ToArray();

        using var response = await client.GetAsync(
            $"/api/projects/{projectId}/analyses/latest/branches.csv?{Filters}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'=SUM(Zoë,2)\"", csv, StringComparison.Ordinal);
        Assert.Contains("Zoë", csv, StringComparison.Ordinal);
        Assert.Contains("refs/heads/feature/café", csv, StringComparison.Ordinal);
        Assert.Contains("Zoë Martin", csv, StringComparison.Ordinal);
        Assert.Equal(expectedReferences, ReadReferences(csv));
    }

    [Theory]
    [InlineData("=danger")]
    [InlineData("+danger")]
    [InlineData("-danger")]
    [InlineData("@danger")]
    [InlineData("  =danger")]
    public void CsvNeutralizesFormulaPrefixes(string author)
    {
        var csv = Encoding.UTF8.GetString(
            SnapshotCsvWriter.Write([CreateSnapshot(author)]));
        Assert.Contains($"\"'{author}\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void CsvEscapesQuotesAndCommas()
    {
        var csv = Encoding.UTF8.GetString(
            SnapshotCsvWriter.Write([CreateSnapshot("Jane \"JJ\", Doe")]));
        Assert.Contains("\"Jane \"\"JJ\"\", Doe\"", csv, StringComparison.Ordinal);
    }

    private static async Task ExcludeFeatureBranchesAsync(HttpClient client, Guid projectId)
    {
        var policy = new
        {
            activeUntilDays = 30,
            inactiveAfterDays = 90,
            excludedPatterns = new[] { "refs/heads/feature/*" },
            protectedPatterns = Array.Empty<string>(),
        };
        using var response = await client.PutAsJsonAsync(
            $"/api/projects/{projectId}/policy/",
            policy);
        response.EnsureSuccessStatusCode();
    }

    private static string?[] ReadReferences(string csv) => csv
        .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
        .Skip(1)
        .Select(line => line[1..line.IndexOf("\",", StringComparison.Ordinal)])
        .Cast<string?>()
        .ToArray();

    private static BranchSnapshotResponse CreateSnapshot(string author) => new()
    {
        Id = Guid.NewGuid(),
        ReferenceName = "refs/heads/feature/formula",
        CommitId = "0123456789abcdef",
        AheadCount = 1,
        BehindCount = 0,
        Relationship = "ReferenceIsAncestorOfBranch",
        LastActivityAtUtc = DateTimeOffset.UnixEpoch,
        TipAuthor = author,
        Topology = "Ahead",
        Activity = "Active",
        Recommendation = "Keep",
        Reason = "Aucune action recommandée",
        IsProtected = false,
        IsExcluded = false,
    };
}
