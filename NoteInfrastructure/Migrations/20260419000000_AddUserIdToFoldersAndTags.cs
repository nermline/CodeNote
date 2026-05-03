using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NoteInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToFoldersAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── folders: додаємо userid ────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name:       "userid",
                table:      "folders",
                type:       "character varying(450)",
                maxLength:  450,
                nullable:   true);

            // ── tags: видаляємо старий унікальний індекс лише по name ──────
            migrationBuilder.DropIndex(
                name:  "tags_name_key",
                table: "tags");

            // ── tags: додаємо userid ───────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name:       "userid",
                table:      "tags",
                type:       "character varying(450)",
                maxLength:  450,
                nullable:   true);

            // ── tags: новий унікальний індекс (name + userid) ──────────────
            migrationBuilder.CreateIndex(
                name:    "tags_name_userid_key",
                table:   "tags",
                columns: new[] { "name", "userid" },
                unique:  true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name:  "tags_name_userid_key",
                table: "tags");

            migrationBuilder.DropColumn(
                name:  "userid",
                table: "tags");

            migrationBuilder.CreateIndex(
                name:   "tags_name_key",
                table:  "tags",
                column: "name",
                unique: true);

            migrationBuilder.DropColumn(
                name:  "userid",
                table: "folders");
        }
    }
}
