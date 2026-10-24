using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupermarketWEB.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                // Los usuarios previos tenían acceso total: conservan ese nivel como administradores.
                defaultValue: "Admin");

            migrationBuilder.AddColumn<string>(
                name: "SecurityStamp",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            // Cada usuario existente recibe un sello aleatorio propio.
            migrationBuilder.Sql("UPDATE \"Users\" SET \"SecurityStamp\" = lower(hex(randomblob(16)));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SecurityStamp",
                table: "Users");
        }
    }
}
