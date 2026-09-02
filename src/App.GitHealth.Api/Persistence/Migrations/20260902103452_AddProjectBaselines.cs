using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.GitHealth.Api.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProjectBaselines : Migration
{
    private const string GuidType = "TEXT";
    private const string IntegerType = "INTEGER";
    private const string TextType = "TEXT";
    private const string BaselinesTable = "ProjectBaselines";
    private const string ProjectsTable = "Projects";
    private const string ProjectColumn = "ProjectId";
    private const string ReferenceColumn = "ReferenceName";
    private const string PositionColumn = "Position";
    private const string LatestColumn = "LastSuccessfulAnalysisId";
    private const string PrimaryKeyName = "PK_ProjectBaselines";
    private const string ForeignKeyName = "FK_ProjectBaselines_Projects_ProjectId";
    private const string LatestIndex = "IX_ProjectBaselines_LastSuccessfulAnalysisId";
    private const string PositionIndex = "IX_ProjectBaselines_ProjectId_Position";
    private const int ReferenceLength = 1024;

    /// <summary>
    /// Every project already declared exactly one baseline, in Projects.ReferenceName. It
    /// becomes the primary one, position 0, keeping the capture it already pointed at.
    /// </summary>
    private const string BackfillStatement =
        "INSERT INTO ProjectBaselines "
        + "(ProjectId, ReferenceName, Position, LastSuccessfulAnalysisId) "
        + "SELECT Id, ReferenceName, 0, LastSuccessfulAnalysisId "
        + "FROM Projects WHERE ReferenceName IS NOT NULL;";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateBaselinesTable(migrationBuilder);

        migrationBuilder.CreateIndex(
            name: LatestIndex,
            table: BaselinesTable,
            column: LatestColumn);

        migrationBuilder.CreateIndex(
            name: PositionIndex,
            table: BaselinesTable,
            columns: [ProjectColumn, PositionColumn]);

        migrationBuilder.Sql(BackfillStatement);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: BaselinesTable);
    }

    private static void CreateBaselinesTable(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: BaselinesTable,
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: GuidType, nullable: false),
                ReferenceName = table.Column<string>(
                    type: TextType,
                    maxLength: ReferenceLength,
                    nullable: false),
                Position = table.Column<int>(type: IntegerType, nullable: false),
                LastSuccessfulAnalysisId = table.Column<Guid>(
                    type: GuidType,
                    nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    PrimaryKeyName,
                    baseline => new { baseline.ProjectId, baseline.ReferenceName });
                table.ForeignKey(
                    name: ForeignKeyName,
                    column: baseline => baseline.ProjectId,
                    principalTable: ProjectsTable,
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });
    }
}
