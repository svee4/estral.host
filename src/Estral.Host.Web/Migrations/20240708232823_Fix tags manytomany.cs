using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Estral.Host.Web.Migrations
{
    /// <inheritdoc />
    public partial class Fixtagsmanytomany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tag_Contents_ContentId",
                table: "Tag");

            migrationBuilder.DropIndex(
                name: "IX_Tag_ContentId",
                table: "Tag");

            migrationBuilder.DropColumn(
                name: "ContentId",
                table: "Tag");

            migrationBuilder.CreateTable(
                name: "ContentTag",
                columns: table => new
                {
                    ContentsId = table.Column<int>(type: "integer", nullable: false),
                    TagsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTag", x => new { x.ContentsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_ContentTag_Contents_ContentsId",
                        column: x => x.ContentsId,
                        principalTable: "Contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContentTag_Tag_TagsId",
                        column: x => x.TagsId,
                        principalTable: "Tag",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTag_TagsId",
                table: "ContentTag",
                column: "TagsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentTag");

            migrationBuilder.AddColumn<int>(
                name: "ContentId",
                table: "Tag",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tag_ContentId",
                table: "Tag",
                column: "ContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tag_Contents_ContentId",
                table: "Tag",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "Id");
        }
    }
}
