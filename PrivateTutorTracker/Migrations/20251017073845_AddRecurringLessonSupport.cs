using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateTutorTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringLessonSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TekrarGrupId",
                table: "Lessons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TekrarPeriyodu",
                table: "Lessons",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TekrarSirasi",
                table: "Lessons",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TekrarliMi",
                table: "Lessons",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TekrarGrupId",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "TekrarPeriyodu",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "TekrarSirasi",
                table: "Lessons");

            migrationBuilder.DropColumn(
                name: "TekrarliMi",
                table: "Lessons");
        }
    }
}
