using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueNamesConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Folders_Name_Parentfolderid",
                table: "folders",
                columns: new[] { "name", "parentfolderid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_Name_Folderid",
                table: "files",
                columns: new[] { "name", "folderid" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Folders_Name_Parentfolderid",
                table: "folders");

            migrationBuilder.DropIndex(
                name: "IX_Files_Name_Folderid",
                table: "files");
        }
    }
}
