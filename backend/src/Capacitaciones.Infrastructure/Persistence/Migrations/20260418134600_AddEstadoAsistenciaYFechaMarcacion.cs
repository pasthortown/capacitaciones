using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoAsistenciaYFechaMarcacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoAsistencia",
                schema: "dbo",
                table: "Asistente",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaMarcacionAsistencia",
                schema: "dbo",
                table: "Asistente",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoAsistencia",
                schema: "dbo",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "FechaMarcacionAsistencia",
                schema: "dbo",
                table: "Asistente");
        }
    }
}
