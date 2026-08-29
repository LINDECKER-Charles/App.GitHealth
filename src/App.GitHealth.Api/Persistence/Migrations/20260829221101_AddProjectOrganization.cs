using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.GitHealth.Api.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProjectOrganization : Migration
{
    private const string IntegerType = "INTEGER";
    private const string TextType = "TEXT";
    private const string GroupIndex = "IX_Projects_GroupName";
    private const string GroupColumn = "GroupName";
    private const string FavoriteColumn = "IsFavorite";
    private const string ProjectsTable = "Projects";
    private const int GroupNameLength = 60;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: GroupColumn,
            table: ProjectsTable,
            type: TextType,
            maxLength: GroupNameLength,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: FavoriteColumn,
            table: ProjectsTable,
            type: IntegerType,
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: GroupIndex,
            table: ProjectsTable,
            column: GroupColumn);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: GroupIndex, table: ProjectsTable);
        migrationBuilder.DropColumn(name: GroupColumn, table: ProjectsTable);
        migrationBuilder.DropColumn(name: FavoriteColumn, table: ProjectsTable);
    }
}
