using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chat.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseCloudinaryForChatMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentPublicId",
                table: "Messages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentResourceType",
                table: "Messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentStorageProvider",
                table: "Messages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentPublicId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "AttachmentResourceType",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "AttachmentStorageProvider",
                table: "Messages");
        }
    }
}
