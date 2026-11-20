using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Umbraco.Cms.Persistence.EFCore.MariaDb.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No database or table character set: naming only a character set resets the database's
            // collation to that character set's default. The tables inherit the database default instead.
            migrationBuilder.CreateTable(
                name: "umbracoOpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    ApplicationType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "longtext", nullable: true),
                    ClientType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "longtext", nullable: true),
                    DisplayNames = table.Column<string>(type: "longtext", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "longtext", nullable: true),
                    Permissions = table.Column<string>(type: "longtext", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "longtext", nullable: true),
                    Properties = table.Column<string>(type: "longtext", nullable: true),
                    RedirectUris = table.Column<string>(type: "longtext", nullable: true),
                    Requirements = table.Column<string>(type: "longtext", nullable: true),
                    Settings = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoOpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoOpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "longtext", nullable: true),
                    Descriptions = table.Column<string>(type: "longtext", nullable: true),
                    DisplayName = table.Column<string>(type: "longtext", nullable: true),
                    DisplayNames = table.Column<string>(type: "longtext", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "longtext", nullable: true),
                    Resources = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoOpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "umbracoOpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    ApplicationId = table.Column<string>(type: "varchar(255)", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Properties = table.Column<string>(type: "longtext", nullable: true),
                    Scopes = table.Column<string>(type: "longtext", nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoOpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_umbracoOpenIddictAuthorizations_umbracoOpenIddictApplication~",
                        column: x => x.ApplicationId,
                        principalTable: "umbracoOpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "umbracoOpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    ApplicationId = table.Column<string>(type: "varchar(255)", nullable: true),
                    AuthorizationId = table.Column<string>(type: "varchar(255)", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Payload = table.Column<string>(type: "longtext", nullable: true),
                    Properties = table.Column<string>(type: "longtext", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReferenceId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_umbracoOpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_umbracoOpenIddictTokens_umbracoOpenIddictApplications_Applic~",
                        column: x => x.ApplicationId,
                        principalTable: "umbracoOpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_umbracoOpenIddictTokens_umbracoOpenIddictAuthorizations_Auth~",
                        column: x => x.AuthorizationId,
                        principalTable: "umbracoOpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictApplications_ClientId",
                table: "umbracoOpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictAuthorizations_ApplicationId_Status_Subject~",
                table: "umbracoOpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictScopes_Name",
                table: "umbracoOpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "umbracoOpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" })
                .Annotation("MySql:IndexPrefixLength", new[] { 0, 0, 313, 0 });

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictTokens_AuthorizationId",
                table: "umbracoOpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_umbracoOpenIddictTokens_ReferenceId",
                table: "umbracoOpenIddictTokens",
                column: "ReferenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "umbracoOpenIddictScopes");

            migrationBuilder.DropTable(
                name: "umbracoOpenIddictTokens");

            migrationBuilder.DropTable(
                name: "umbracoOpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "umbracoOpenIddictApplications");
        }
    }
}
