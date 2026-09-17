using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrivateTutorTracker.Migrations
{
    /// <inheritdoc />
    public partial class AddZoomMeetingIdToLesson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ZoomMeetingId",
                table: "Lessons",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ZoomMeetingId",
                table: "Lessons");
        }
    }
}
