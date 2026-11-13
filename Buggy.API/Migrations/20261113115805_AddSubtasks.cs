using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buggy.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedDate",
                table: "BacklogItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "BacklogItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "BacklogItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_ParentId",
                table: "BacklogItems",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_BacklogItems_BacklogItems_ParentId",
                table: "BacklogItems",
                column: "ParentId",
                principalTable: "BacklogItems",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BacklogItems_BacklogItems_ParentId",
                table: "BacklogItems");

            migrationBuilder.DropIndex(
                name: "IX_BacklogItems_ParentId",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "ArchivedDate",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "BacklogItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "BacklogItems");
        }
    }
}
