using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncuestaSatisfaccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La columna Email del Responsable fue incorporada a la entidad en el commit
            // "feat(responsables): campo Email obligatorio" sin su migración acompañante.
            // En producción la columna ya existe (agregada manualmente) y tiene datos
            // reales; en dev no existe todavía. Emitimos el ALTER TABLE condicional para
            // que esta migración sea idempotente en ambos escenarios.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE Name = 'Email'
                      AND Object_ID = Object_ID(N'dbo.Responsable')
                )
                BEGIN
                    ALTER TABLE [dbo].[Responsable]
                    ADD [Email] NVARCHAR(320) NOT NULL CONSTRAINT DF_Responsable_Email_Tmp DEFAULT('');
                    ALTER TABLE [dbo].[Responsable] DROP CONSTRAINT DF_Responsable_Email_Tmp;
                END
            ");

            migrationBuilder.CreateTable(
                name: "PreguntaEncuesta",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoActividadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreguntaEncuesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreguntaEncuesta_TipoActividad_TipoActividadId",
                        column: x => x.TipoActividadId,
                        principalSchema: "dbo",
                        principalTable: "TipoActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RespuestaEncuesta",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsistenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreguntaEncuestaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Valor = table.Column<int>(type: "int", nullable: false),
                    FechaRespuesta = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespuestaEncuesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespuestaEncuesta_Asistente_AsistenteId",
                        column: x => x.AsistenteId,
                        principalSchema: "dbo",
                        principalTable: "Asistente",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespuestaEncuesta_PreguntaEncuesta_PreguntaEncuestaId",
                        column: x => x.PreguntaEncuestaId,
                        principalSchema: "dbo",
                        principalTable: "PreguntaEncuesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PreguntaEncuesta_TipoActividadId",
                schema: "dbo",
                table: "PreguntaEncuesta",
                column: "TipoActividadId");

            migrationBuilder.CreateIndex(
                name: "IX_RespuestaEncuesta_PreguntaEncuestaId",
                schema: "dbo",
                table: "RespuestaEncuesta",
                column: "PreguntaEncuestaId");

            migrationBuilder.CreateIndex(
                name: "UX_RespuestaEncuesta_Asistente_Pregunta",
                schema: "dbo",
                table: "RespuestaEncuesta",
                columns: new[] { "AsistenteId", "PreguntaEncuestaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespuestaEncuesta",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PreguntaEncuesta",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "dbo",
                table: "Responsable");
        }
    }
}
