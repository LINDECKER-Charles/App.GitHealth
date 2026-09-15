using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.GitHealth.Api.Persistence.Migrations;

/// <summary>
/// Adds the four columns a scheduled scan needs. Nothing is backfilled: every project already
/// stored keeps no schedule, which is what it had, and the switch stays off until someone
/// turns it on. The index serves the one read the scheduler makes on each of its ticks.
/// </summary>
/// <inheritdoc />
public partial class AddProjectScanSchedules : Migration
{
    private const string IntegerType = "INTEGER";
    private const string TextType = "TEXT";
    private const string ProjectsTable = "Projects";
    private const string EnabledColumn = "IsScheduleEnabled";
    private const string CronColumn = "ScheduleCron";
    private const string ChangedColumn = "ScheduleChangedAtUtc";
    private const string LastRunColumn = "ScheduleLastRunAtUtc";
    private const string EnabledIndex = "IX_Projects_IsScheduleEnabled";
    private const int CronLength = 120;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: EnabledColumn,
            table: ProjectsTable,
            type: IntegerType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: CronColumn,
            table: ProjectsTable,
            type: TextType,
            maxLength: CronLength,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: ChangedColumn,
            table: ProjectsTable,
            type: IntegerType,
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: LastRunColumn,
            table: ProjectsTable,
            type: IntegerType,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: EnabledIndex,
            table: ProjectsTable,
            column: EnabledColumn);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: EnabledIndex, table: ProjectsTable);
        migrationBuilder.DropColumn(name: EnabledColumn, table: ProjectsTable);
        migrationBuilder.DropColumn(name: CronColumn, table: ProjectsTable);
        migrationBuilder.DropColumn(name: ChangedColumn, table: ProjectsTable);
        migrationBuilder.DropColumn(name: LastRunColumn, table: ProjectsTable);
    }
}
