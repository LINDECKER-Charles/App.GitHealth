using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.GitHealth.Api.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAssistantConversations : Migration
{
    private const string GuidType = "TEXT";
    private const string IntegerType = "INTEGER";
    private const string TextType = "TEXT";
    private const string BooleanType = "INTEGER";
    private const string ConversationsTable = "AssistantConversations";
    private const string MessagesTable = "AssistantMessages";
    private const string AnalysisRunsTable = "AnalysisRuns";
    private const string ProjectsTable = "Projects";
    private const string IdColumn = "Id";
    private const string AnalysisRunColumn = "AnalysisRunId";
    private const string ConversationColumn = "ConversationId";
    private const string PositionColumn = "Position";
    private const string UpdatedColumn = "UpdatedAtUtc";
    private const string ConsentColumn = "AssistantConsentAtUtc";
    private const string ConversationsKey = "PK_AssistantConversations";
    private const string MessagesKey = "PK_AssistantMessages";
    private const string CaptureForeignKey =
        "FK_AssistantConversations_AnalysisRuns_AnalysisRunId";
    private const string ThreadForeignKey =
        "FK_AssistantMessages_AssistantConversations_ConversationId";
    private const string CaptureIndex = "IX_AssistantConversations_AnalysisRunId";
    private const string UpdatedIndex = "IX_AssistantConversations_UpdatedAtUtc";
    private const string OrderIndex = "IX_AssistantMessages_ConversationId_Position";
    private const int AgentIdLength = 40;
    private const int AgentNameLength = 100;
    private const int TitleLength = 300;
    private const int RoleLength = 10;
    private const int StatusLength = 20;
    private const int EffortLength = 20;
    private const int CommandLength = 2000;
    private const int FailureCodeLength = 100;
    private const int FailureMessageLength = 2000;

    private static readonly string[] OrderColumns = [ConversationColumn, PositionColumn];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.AddColumn<long>(
            name: ConsentColumn,
            table: ProjectsTable,
            type: IntegerType,
            nullable: true);
        CreateConversations(migrationBuilder);
        CreateMessages(migrationBuilder);
        AddIndexes(migrationBuilder);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);
        migrationBuilder.DropTable(name: MessagesTable);
        migrationBuilder.DropTable(name: ConversationsTable);
        migrationBuilder.DropColumn(name: ConsentColumn, table: ProjectsTable);
    }

    /// <summary>
    /// A conversation hangs off the capture it read, so deleting that capture takes the
    /// conversation with it rather than leaving an answer about measurements that are gone.
    /// </summary>
    private static void CreateConversations(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: ConversationsTable,
            columns: table => new
            {
                Id = table.Column<Guid>(type: GuidType, nullable: false),
                AnalysisRunId = table.Column<Guid>(type: GuidType, nullable: false),
                AgentId = table.Column<string>(
                    type: TextType,
                    maxLength: AgentIdLength,
                    nullable: false),
                AgentName = table.Column<string>(
                    type: TextType,
                    maxLength: AgentNameLength,
                    nullable: false),
                Title = table.Column<string>(
                    type: TextType,
                    maxLength: TitleLength,
                    nullable: false),
                BranchCount = table.Column<int>(type: IntegerType, nullable: false),
                StartedAtUtc = table.Column<long>(type: IntegerType, nullable: false),
                UpdatedAtUtc = table.Column<long>(type: IntegerType, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(ConversationsKey, conversation => conversation.Id);
                table.ForeignKey(
                    name: CaptureForeignKey,
                    column: conversation => conversation.AnalysisRunId,
                    principalTable: AnalysisRunsTable,
                    principalColumn: IdColumn,
                    onDelete: ReferentialAction.Cascade);
            });
    }

    private static void CreateMessages(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: MessagesTable,
            columns: table => new
            {
                Id = table.Column<Guid>(type: GuidType, nullable: false),
                ConversationId = table.Column<Guid>(type: GuidType, nullable: false),
                Position = table.Column<int>(type: IntegerType, nullable: false),
                Role = table.Column<string>(
                    type: TextType,
                    maxLength: RoleLength,
                    nullable: false),
                Text = table.Column<string>(type: TextType, nullable: false),
                WrittenAtUtc = table.Column<long>(type: IntegerType, nullable: false),
                Status = table.Column<string>(
                    type: TextType,
                    maxLength: StatusLength,
                    nullable: true),
                Effort = table.Column<string>(
                    type: TextType,
                    maxLength: EffortLength,
                    nullable: true),
                CommandLine = table.Column<string>(
                    type: TextType,
                    maxLength: CommandLength,
                    nullable: true),
                FailureCode = table.Column<string>(
                    type: TextType,
                    maxLength: FailureCodeLength,
                    nullable: true),
                FailureMessage = table.Column<string>(
                    type: TextType,
                    maxLength: FailureMessageLength,
                    nullable: true),
                DurationMs = table.Column<int>(type: IntegerType, nullable: true),
                IsTruncated = table.Column<bool>(type: BooleanType, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(MessagesKey, message => message.Id);
                table.ForeignKey(
                    name: ThreadForeignKey,
                    column: message => message.ConversationId,
                    principalTable: ConversationsTable,
                    principalColumn: IdColumn,
                    onDelete: ReferentialAction.Cascade);
            });
    }

    private static void AddIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: CaptureIndex,
            table: ConversationsTable,
            column: AnalysisRunColumn);

        migrationBuilder.CreateIndex(
            name: UpdatedIndex,
            table: ConversationsTable,
            column: UpdatedColumn);

        migrationBuilder.CreateIndex(
            name: OrderIndex,
            table: MessagesTable,
            columns: OrderColumns,
            unique: true);
    }
}
