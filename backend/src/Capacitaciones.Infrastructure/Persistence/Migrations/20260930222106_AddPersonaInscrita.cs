using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonaInscrita : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersonaInscrita",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identificacion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AreaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailUsuario = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Firma = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonaInscrita", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonaInscrita_Area_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "dbo",
                        principalTable: "Area",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonaInscrita_AreaId",
                schema: "dbo",
                table: "PersonaInscrita",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "UX_PersonaInscrita_Identificacion",
                schema: "dbo",
                table: "PersonaInscrita",
                column: "Identificacion",
                unique: true);

            // Backfill: una persona por identificación, tomando su inscripción más reciente.
            migrationBuilder.Sql(@"
INSERT INTO dbo.PersonaInscrita (Id, Identificacion, Nombres, Apellidos, AreaId, EmailUsuario, Firma, FechaCreacion, FechaActualizacion)
SELECT NEWID(), a.Identificacion, a.Nombres, a.Apellidos, a.AreaId, a.EmailUsuario, NULLIF(a.Firma, ''), a.FechaInscripcion, a.FechaInscripcion
FROM (
    SELECT Identificacion, Nombres, Apellidos, AreaId, EmailUsuario, Firma, FechaInscripcion,
           ROW_NUMBER() OVER (PARTITION BY Identificacion ORDER BY FechaInscripcion DESC) AS rn
    FROM dbo.Asistente
) a
WHERE a.rn = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PersonaInscrita",
                schema: "dbo");
        }
    }
}
