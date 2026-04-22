using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTiposPregunta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Valor",
                schema: "dbo",
                table: "RespuestaEncuesta");

            migrationBuilder.AddColumn<string>(
                name: "Respuesta",
                schema: "dbo",
                table: "RespuestaEncuesta",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OpcionesJson",
                schema: "dbo",
                table: "PreguntaEncuesta",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoPregunta",
                schema: "dbo",
                table: "PreguntaEncuesta",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Respuesta",
                schema: "dbo",
                table: "RespuestaEncuesta");

            migrationBuilder.DropColumn(
                name: "OpcionesJson",
                schema: "dbo",
                table: "PreguntaEncuesta");

            migrationBuilder.DropColumn(
                name: "TipoPregunta",
                schema: "dbo",
                table: "PreguntaEncuesta");

            migrationBuilder.AddColumn<int>(
                name: "Valor",
                schema: "dbo",
                table: "RespuestaEncuesta",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
