using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppTemplate.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OAuthGrantId",
                table: "AccessTokens",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OAuthClients",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrantTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TokenEndpointAuthMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsMetadataDocumentClient = table.Column<bool>(type: "bit", nullable: false),
                    MetadataDocumentExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MetadataDocumentFetchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RedirectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthGrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OAuthGrants_OAuthClients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "OAuthClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OAuthAuthorizationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RedirectUri = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CodeChallenge = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthAuthorizationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthAuthorizationCodes_OAuthGrants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "OAuthGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OAuthRefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    GrantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthRefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OAuthRefreshTokens_OAuthGrants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "OAuthGrants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokens_OAuthGrantId",
                table: "AccessTokens",
                column: "OAuthGrantId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthAuthorizationCodes_CodeHash",
                table: "OAuthAuthorizationCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthAuthorizationCodes_GrantId",
                table: "OAuthAuthorizationCodes",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthGrants_ClientId",
                table: "OAuthGrants",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthGrants_UserId",
                table: "OAuthGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthRefreshTokens_GrantId",
                table: "OAuthRefreshTokens",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthRefreshTokens_TokenHash",
                table: "OAuthRefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OAuthAuthorizationCodes");

            migrationBuilder.DropTable(
                name: "OAuthRefreshTokens");

            migrationBuilder.DropTable(
                name: "OAuthGrants");

            migrationBuilder.DropTable(
                name: "OAuthClients");

            migrationBuilder.DropIndex(
                name: "IX_AccessTokens_OAuthGrantId",
                table: "AccessTokens");

            migrationBuilder.DropColumn(
                name: "OAuthGrantId",
                table: "AccessTokens");
        }
    }
}
