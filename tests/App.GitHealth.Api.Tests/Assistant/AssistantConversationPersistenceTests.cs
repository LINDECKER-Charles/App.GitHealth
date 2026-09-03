using App.GitHealth.Api.Persistence;
using App.GitHealth.Api.Persistence.Entities;
using App.GitHealth.Api.Persistence.Models;
using App.GitHealth.Api.Persistence.Models.Assistant;
using App.GitHealth.Api.Persistence.Repositories;
using App.GitHealth.Api.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace App.GitHealth.Api.Tests.Assistant;

public sealed class AssistantConversationPersistenceTests
{
    private const string AgentId = "claude";
    private const string AgentName = "Claude Code";
    private const string CompletedStatus = "Completed";

    private static readonly DateTimeOffset Start =
        new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ATurnIsStoredAndReadBackAsAQuestionThenItsAnswer()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var captureId = await CaptureAsync(database, await ReadProjectIdAsync(database), Start);
        var conversationId = Guid.NewGuid();
        await using var scope = database.CreateScope();
        var conversations = Conversations(scope);

        var kept = await conversations.AppendAsync(
            Turn(conversationId, captureId, 1),
            CancellationToken.None);

        var stored = await conversations.GetAsync(conversationId, CancellationToken.None);
        var messages = stored!.Messages.OrderBy(message => message.Position).ToList();
        Assert.True(kept);
        Assert.Equal(captureId, stored.AnalysisRunId);
        Assert.Equal(AgentName, stored.AgentName);
        Assert.Equal(Question(1), stored.Title);
        Assert.Equal(
            [AssistantMessageEntity.UserRole, AssistantMessageEntity.AgentRole],
            messages.Select(message => message.Role));
        Assert.Equal(Question(1), messages[0].Text);
        Assert.Equal(Answer(1), messages[1].Text);
        Assert.Equal(CompletedStatus, messages[1].Status);
    }

    /// <summary>
    /// A follow-up names the thread it belongs to, so the panel shows one conversation
    /// growing rather than a new thread per question.
    /// </summary>
    [Fact]
    public async Task ASecondTurnLandsInTheSameConversationRatherThanOpeningAnother()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        var captureId = await CaptureAsync(database, projectId, Start);
        var conversationId = Guid.NewGuid();
        await using var scope = database.CreateScope();
        var conversations = Conversations(scope);
        await conversations.AppendAsync(Turn(conversationId, captureId, 1), default);

        await conversations.AppendAsync(Turn(conversationId, captureId, 2), default);

        var stored = await conversations.GetAsync(conversationId, CancellationToken.None);
        Assert.Equal(1, await conversations.CountAsync(projectId, CancellationToken.None));
        Assert.Equal(
            [Question(1), Answer(1), Question(2), Answer(2)],
            stored!.Messages.OrderBy(message => message.Position)
                .Select(message => message.Text));
        var summary = Assert.Single(
            await conversations.ListAsync(projectId, CancellationToken.None));
        Assert.Equal(2, summary.AnswerCount);
        Assert.Equal(Question(1), summary.Title);
    }

    /// <summary>
    /// A conversation is only meaningful next to the measurements it argued about, so the
    /// capture going takes the thread and its messages with it.
    /// </summary>
    [Fact]
    public async Task DeletingTheCaptureCascadesToItsConversationAndItsMessages()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        var keptId = await CaptureAsync(database, projectId, Start);
        var deletedId = await CaptureAsync(database, projectId, Start.AddHours(1));
        await AppendAsync(database, keptId);
        await AppendAsync(database, deletedId);
        var before = await CountRowsAsync(database);
        await using var scope = database.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IAnalysisRepository>()
            .DeleteAsync(deletedId, CancellationToken.None);

        Assert.Equal(new RowCounts(2, 2, 4), before);
        Assert.Equal(new RowCounts(1, 1, 2), await CountRowsAsync(database));
    }

    /// <summary>
    /// Emptying the history is the whole point of the screen offering it: the threads go,
    /// the captures they read stay, and the count is what tells the user something went.
    /// </summary>
    [Fact]
    public async Task PurgingAProjectRemovesEveryConversationAndReportsHowMany()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        var firstId = await CaptureAsync(database, projectId, Start);
        var secondId = await CaptureAsync(database, projectId, Start.AddHours(1));
        await AppendAsync(database, firstId);
        await AppendAsync(database, secondId);
        await using var scope = database.CreateScope();
        var conversations = Conversations(scope);

        var purged = await conversations.PurgeAsync(projectId, CancellationToken.None);

        Assert.Equal(2, purged);
        Assert.Equal(0, await conversations.CountAsync(projectId, CancellationToken.None));
        Assert.Equal(new RowCounts(2, 0, 0), await CountRowsAsync(database));
    }

    /// <summary>
    /// Consent is a moment rather than a flag, because the interface shows when it was
    /// given. Withdrawing puts the project back to "never allowed" instead of recording a
    /// refusal that would read as a different state.
    /// </summary>
    [Fact]
    public async Task ConsentIsKeptAsAMomentAndWithdrawingItClearsThatMoment()
    {
        await using var database = await CreateDatabaseWithProjectAsync();
        var projectId = await ReadProjectIdAsync(database);
        await using var scope = database.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var granted = Start.AddMinutes(5);

        await projects.SetAssistantConsentAsync(Consent(projectId, granted), default);

        var project = await projects.GetAsync(projectId, CancellationToken.None);
        Assert.Equal(granted, project!.AssistantConsentAtUtc);
        await projects.SetAssistantConsentAsync(Consent(projectId, null), default);
        var withdrawn = await projects.GetAsync(projectId, CancellationToken.None);
        Assert.Null(withdrawn!.AssistantConsentAtUtc);
    }

    private static IAssistantConversationRepository Conversations(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IAssistantConversationRepository>();

    private static async Task<SqliteTestDatabase> CreateDatabaseWithProjectAsync()
    {
        var database = await SqliteTestDatabase.CreateAsync();
        await using var scope = database.CreateScope();
        var path = Path.Combine(database.RootPath, "repository");
        await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .AddAsync(PersistenceTestData.CreateProject(path), Start, CancellationToken.None);
        return database;
    }

    private static async Task<Guid> ReadProjectIdAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var projects = await scope.ServiceProvider.GetRequiredService<IProjectRepository>()
            .ListAsync(CancellationToken.None);
        return Assert.Single(projects).Id;
    }

    private static async Task<Guid> CaptureAsync(
        SqliteTestDatabase database,
        Guid projectId,
        DateTimeOffset startedAt)
    {
        await using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAnalysisRepository>();
        var target = PersistenceTestData.PrimaryTarget(projectId);
        var id = await repository.StartAsync(target, startedAt, CancellationToken.None);
        var completion = new AnalysisCompletion(
            PersistenceTestData.CreateScan(startedAt.AddMinutes(1)),
            startedAt.AddMinutes(2));
        await repository.CompleteAsync(id, completion, CancellationToken.None);
        return id;
    }

    private static async Task AppendAsync(SqliteTestDatabase database, Guid captureId)
    {
        await using var scope = database.CreateScope();
        await Conversations(scope).AppendAsync(
            Turn(Guid.NewGuid(), captureId, 1),
            CancellationToken.None);
    }

    private static async Task<RowCounts> CountRowsAsync(SqliteTestDatabase database)
    {
        await using var scope = database.CreateScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<GitHealthDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        return new RowCounts(
            await context.AnalysisRuns.CountAsync(),
            await context.AssistantConversations.CountAsync(),
            await context.AssistantMessages.CountAsync());
    }

    private static AssistantTurnRecord Turn(Guid conversationId, Guid captureId, int number) =>
        new()
        {
            ConversationId = conversationId,
            AnalysisRunId = captureId,
            AgentId = AgentId,
            AgentName = AgentName,
            Effort = "medium",
            CommandLine = "claude --print --mcp-config <single-use-token>",
            BranchCount = 3,
            Question = Question(number),
            AskedAtUtc = Start.AddMinutes(number),
            SettledAtUtc = Start.AddMinutes(number).AddSeconds(20),
            Status = CompletedStatus,
            Answer = Answer(number),
        };

    private static AssistantConsentUpdate Consent(Guid projectId, DateTimeOffset? granted) =>
        new()
        {
            ProjectId = projectId,
            GrantedAtUtc = granted,
            ChangedAtUtc = Start.AddMinutes(5),
        };

    private static string Question(int number) => $"Which branches can I clean up? ({number})";

    private static string Answer(int number) => $"Two of them, and here is why ({number}).";

    private sealed record RowCounts(int Runs, int Conversations, int Messages);
}
