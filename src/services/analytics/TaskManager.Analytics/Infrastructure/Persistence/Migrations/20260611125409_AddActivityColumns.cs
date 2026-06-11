using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManager.Analytics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "task_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "TaskTitle",
                table: "task_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_events_BoardId_OccurredAt",
                table: "task_events",
                columns: new[] { "BoardId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_events_BoardId_OccurredAt",
                table: "task_events");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "task_events");

            migrationBuilder.DropColumn(
                name: "TaskTitle",
                table: "task_events");
        }
    }
}
