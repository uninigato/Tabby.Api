using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabby.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectMessageReadAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "DirectMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE DirectMessages SET ReadAt = SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "DirectMessages");
        }
    }
}
