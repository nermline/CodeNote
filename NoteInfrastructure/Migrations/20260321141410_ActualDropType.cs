using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ActualDropType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
