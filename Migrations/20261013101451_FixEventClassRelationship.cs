
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTracker.Migrations
{
    public partial class FixEventClassRelationship : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove old Points column.
            migrationBuilder.DropColumn(
                name: "Points",
                table: "Events");

            // Add ClassroomId as nullable temporarily.
            migrationBuilder.AddColumn<int>(
                name: "ClassroomId",
                table: "Events",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "EventParticipations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing events have no selected class.
            // Assign them to an existing class, if one exists.
            migrationBuilder.Sql(@"
                UPDATE Events
                SET ClassroomId = (SELECT TOP 1 Id FROM Classrooms ORDER BY Id)
                WHERE ClassroomId IS NULL
                  AND EXISTS (SELECT 1 FROM Classrooms);
            ");

            // Keep only events that can be assigned to an existing class.
            migrationBuilder.Sql(@"
                DELETE FROM Events
                WHERE ClassroomId IS NULL;
            ");

            migrationBuilder.AlterColumn<int>(
                name: "ClassroomId",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_ClassroomId",
                table: "Events",
                column: "ClassroomId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Classrooms_ClassroomId",
                table: "Events",
                column: "ClassroomId",
                principalTable: "Classrooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_Classrooms_ClassroomId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_ClassroomId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "EventParticipations");

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}