using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhisperFTPApp.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FtpConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FtpConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BackgroundPathImage = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FtpConnections",
                columns: new[] { "Id", "Address", "LastUsed", "Name", "Password", "Username" },
                values: new object[] { 1, "ftp://demo.wftpserver.com", new DateTime(2024, 12, 26, 14, 15, 4, 680, DateTimeKind.Local).AddTicks(908), "ftp://demo.wftpserver.com", "demo", "demo" });

            migrationBuilder.InsertData(
                table: "Settings",
                columns: new[] { "Id", "BackgroundPathImage" },
                values: new object[] { 1, "/Assets/Image (3).jpg" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FtpConnections");

            migrationBuilder.DropTable(
                name: "Settings");
        }
    }
}
