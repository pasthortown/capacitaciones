using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLogoYPuntajeMinimoCapacitacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                schema: "dbo",
                table: "Capacitacion",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoPath",
                schema: "dbo",
                table: "Capacitacion",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PuntajeMinimo",
                schema: "dbo",
                table: "Capacitacion",
                type: "decimal(4,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                schema: "dbo",
                table: "Capacitacion");

            migrationBuilder.DropColumn(
                name: "LogoPath",
                schema: "dbo",
                table: "Capacitacion");

            migrationBuilder.DropColumn(
                name: "PuntajeMinimo",
                schema: "dbo",
                table: "Capacitacion");
        }
    }
}
