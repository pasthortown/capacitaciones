using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoEnvioCertificadoAsistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstadoEnvioCertificado",
                schema: "dbo",
                table: "Asistente",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaEnvioCertificado",
                schema: "dbo",
                table: "Asistente",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensajeErrorEnvio",
                schema: "dbo",
                table: "Asistente",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstadoEnvioCertificado",
                schema: "dbo",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "FechaEnvioCertificado",
                schema: "dbo",
                table: "Asistente");

            migrationBuilder.DropColumn(
                name: "MensajeErrorEnvio",
                schema: "dbo",
                table: "Asistente");
        }
    }
}
