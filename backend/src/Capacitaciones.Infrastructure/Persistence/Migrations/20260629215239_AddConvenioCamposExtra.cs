using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvenioCamposExtra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AreaColaborador",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutorizadoPor",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CargoColaborador",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolicitadoPor",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AreaColaborador",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "AutorizadoPor",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "CargoColaborador",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "SolicitadoPor",
                schema: "dbo",
                table: "Convenio");
        }
    }
}
