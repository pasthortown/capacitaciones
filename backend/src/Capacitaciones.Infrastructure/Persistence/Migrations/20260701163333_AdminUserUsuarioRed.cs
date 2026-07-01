using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdminUserUsuarioRed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AdminUser_Email",
                schema: "dbo",
                table: "AdminUser");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Nombres",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioRed",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Backfill: usuarios legados (email/password) → usuario de red = parte local del correo,
            // para que el índice único no colisione y aparezcan en la lista de permitidos.
            migrationBuilder.Sql(
                "UPDATE dbo.AdminUser SET UsuarioRed = LEFT(Email, CHARINDEX('@', Email) - 1) " +
                "WHERE (UsuarioRed IS NULL OR UsuarioRed = '') AND Email LIKE '%@%';");

            migrationBuilder.CreateIndex(
                name: "UX_AdminUser_UsuarioRed",
                schema: "dbo",
                table: "AdminUser",
                column: "UsuarioRed",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_AdminUser_UsuarioRed",
                schema: "dbo",
                table: "AdminUser");

            migrationBuilder.DropColumn(
                name: "UsuarioRed",
                schema: "dbo",
                table: "AdminUser");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Nombres",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "dbo",
                table: "AdminUser",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_AdminUser_Email",
                schema: "dbo",
                table: "AdminUser",
                column: "Email",
                unique: true);
        }
    }
}
