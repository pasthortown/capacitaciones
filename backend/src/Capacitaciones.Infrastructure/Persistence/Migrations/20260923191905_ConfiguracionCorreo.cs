using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracionCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionCorreo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUser = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SmtpPasswordCifrada = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    UsarTls = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RemitenteCorreo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RemitenteNombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CcGlobal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BccGlobal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ActualizadoPor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionCorreo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionNotificacion",
                schema: "dbo",
                columns: table => new
                {
                    Plantilla = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    AsuntoPersonalizado = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActualizadoPor = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ActualizadoEn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionNotificacion", x => x.Plantilla);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "ConfiguracionNotificacion",
                columns: new[] { "Plantilla", "Activo", "ActualizadoEn", "ActualizadoPor", "AsuntoPersonalizado", "Nombre" },
                values: new object[,]
                {
                    { "capacitador_descripcion", true, null, null, null, "Capacitador: cargar información del curso" },
                    { "capacitador_pase_lista", true, null, null, null, "Capacitador: pase de lista" },
                    { "certificado_participante", true, null, null, null, "Certificado al participante" },
                    { "encuesta_satisfaccion", true, null, null, null, "Encuesta de satisfacción" },
                    { "invitacion_inscripcion", true, null, null, null, "Invitación de inscripción" },
                    { "recordatorio_evento_iniciado", true, null, null, null, "Aviso: capacitación iniciada" },
                    { "recordatorio_inicio_proximo", true, null, null, null, "Recordatorio: capacitación por iniciar" },
                    { "registro_asistencia_admin", true, null, null, null, "Reporte de asistencia al admin" },
                    { "responsable_firma", true, null, null, null, "Responsable: carga de datos y firma" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionCorreo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ConfiguracionNotificacion",
                schema: "dbo");
        }
    }
}
