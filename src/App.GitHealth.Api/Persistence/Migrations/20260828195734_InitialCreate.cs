using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.GitHealth.Api.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    private const string IntegerType = "INTEGER";
    private const string Text = "TEXT";
    private const string TextType = Text;
    private static readonly string[] AnalysisIndexColumns = ["ProjectId", "StartedAtUtc"];
    private static readonly string[] BranchIndexColumns = ["AnalysisRunId", "ReferenceName"];
    private static readonly string[] ContributorIndexColumns =
        ["BranchSnapshotId", "Name", "Email"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateProjects(migrationBuilder);
        CreateAnalysisRuns(migrationBuilder);
        CreateBranchSnapshots(migrationBuilder);
        CreateContributorSnapshots(migrationBuilder);
        AddRelationships(migrationBuilder);
        AddAnalysisIndexes(migrationBuilder);
        AddProjectIndexes(migrationBuilder);
    }

    private static void CreateProjects(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: TextType, nullable: false),
                DisplayName = table.Column<string>(
                    type: TextType, maxLength: 200, nullable: false),
                RepositoryPath = table.Column<string>(
                    type: TextType, maxLength: 2048, nullable: false),
                IsRepositoryAccessible = table.Column<bool>(
                    type: IntegerType, nullable: false),
                CreatedAtUtc = table.Column<long>(type: IntegerType, nullable: false),
                UpdatedAtUtc = table.Column<long>(type: IntegerType, nullable: false),
                ReferenceName = table.Column<string>(
                    type: TextType, maxLength: 1024, nullable: true),
                BranchNamespace = table.Column<string>(
                    type: TextType, maxLength: 1024, nullable: false),
                ActiveUntilDays = table.Column<int>(type: IntegerType, nullable: false),
                InactiveAfterDays = table.Column<int>(type: IntegerType, nullable: false),
                ExcludedPatternsJson = table.Column<string>(type: TextType, nullable: false),
                ProtectedPatternsJson = table.Column<string>(type: TextType, nullable: false),
                LastSuccessfulAnalysisId = table.Column<Guid>(type: TextType, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_Projects", item => item.Id));
    }

    private static void CreateAnalysisRuns(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AnalysisRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: TextType, nullable: false),
                ProjectId = table.Column<Guid>(type: TextType, nullable: false),
                Status = table.Column<string>(type: Text, maxLength: 20, nullable: false),
                StartedAtUtc = table.Column<long>(type: IntegerType, nullable: false),
                CompletedAtUtc = table.Column<long>(type: IntegerType, nullable: true),
                CapturedAtUtc = table.Column<long>(type: IntegerType, nullable: true),
                GitVersion = table.Column<string>(type: Text, maxLength: 100, nullable: true),
                ReferenceName = table.Column<string>(type: Text, maxLength: 1024, nullable: false),
                ReferenceCommit = table.Column<string>(type: Text, maxLength: 128, nullable: true),
                BranchNamespace = table.Column<string>(
                    type: TextType, maxLength: 1024, nullable: false),
                ActiveUntilDays = table.Column<int>(type: IntegerType, nullable: false),
                InactiveAfterDays = table.Column<int>(type: IntegerType, nullable: false),
                ExcludedPatternsJson = table.Column<string>(type: TextType, nullable: false),
                ProtectedPatternsJson = table.Column<string>(type: TextType, nullable: false),
                FailureCode = table.Column<string>(type: Text, maxLength: 100, nullable: true),
                FailureMessage = table.Column<string>(type: Text, maxLength: 2000, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_AnalysisRuns", item => item.Id));
    }

    private static void CreateBranchSnapshots(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BranchSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: TextType, nullable: false),
                AnalysisRunId = table.Column<Guid>(type: TextType, nullable: false),
                ReferenceName = table.Column<string>(
                    type: TextType, maxLength: 1024, nullable: false),
                CommitId = table.Column<string>(
                    type: TextType, maxLength: 128, nullable: false),
                AheadCount = table.Column<int>(type: IntegerType, nullable: false),
                BehindCount = table.Column<int>(type: IntegerType, nullable: false),
                Relationship = table.Column<string>(
                    type: TextType, maxLength: 40, nullable: false),
                LastActivityAtUtc = table.Column<long>(type: IntegerType, nullable: true),
                TipAuthor = table.Column<string>(
                    type: TextType, maxLength: 500, nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_BranchSnapshots", item => item.Id));
    }

    private static void CreateContributorSnapshots(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ContributorSnapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: TextType, nullable: false),
                BranchSnapshotId = table.Column<Guid>(type: TextType, nullable: false),
                Name = table.Column<string>(
                    type: TextType, maxLength: 500, nullable: false),
                Email = table.Column<string>(
                    type: TextType, maxLength: 500, nullable: false),
                CommitCount = table.Column<int>(type: IntegerType, nullable: false),
            },
            constraints: table =>
                table.PrimaryKey("PK_ContributorSnapshots", item => item.Id));
    }

    private static void AddRelationships(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddForeignKey(
            name: "FK_AnalysisRuns_Projects_ProjectId",
            table: "AnalysisRuns", column: "ProjectId",
            principalTable: "Projects", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(
            name: "FK_BranchSnapshots_AnalysisRuns_AnalysisRunId",
            table: "BranchSnapshots", column: "AnalysisRunId",
            principalTable: "AnalysisRuns", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(
            name: "FK_ContributorSnapshots_BranchSnapshots_BranchSnapshotId",
            table: "ContributorSnapshots", column: "BranchSnapshotId",
            principalTable: "BranchSnapshots", principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    private static void AddAnalysisIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_AnalysisRuns_ProjectId_StartedAtUtc",
            table: "AnalysisRuns", columns: AnalysisIndexColumns);
        migrationBuilder.CreateIndex(
            name: "IX_BranchSnapshots_AnalysisRunId_ReferenceName",
            table: "BranchSnapshots", columns: BranchIndexColumns,
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ContributorSnapshots_BranchSnapshotId_Name_Email",
            table: "ContributorSnapshots", columns: ContributorIndexColumns,
            unique: true);
    }

    private static void AddProjectIndexes(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Projects_LastSuccessfulAnalysisId",
            table: "Projects", column: "LastSuccessfulAnalysisId");
        migrationBuilder.CreateIndex(
            name: "IX_Projects_RepositoryPath",
            table: "Projects", column: "RepositoryPath",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ContributorSnapshots");
        migrationBuilder.DropTable(name: "BranchSnapshots");
        migrationBuilder.DropTable(name: "AnalysisRuns");
        migrationBuilder.DropTable(name: "Projects");
    }
}
