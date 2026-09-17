using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateTutorTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonTypeAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DersNotu",
                table: "Lessons",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tur",
                table: "Lessons",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DersNotu",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "Tur",
                table: "Lessons");
        }
    }
}
