using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Refactor destructivo: <c>Responsable</c> deja de ser hijo de <c>Capacitacion</c> y se
    /// convierte en catálogo global con relación N–N vía la pivote <c>CapacitacionResponsable</c>.
    /// En el Up() se dropea la tabla <c>Responsable</c> vieja y se recrea con el nuevo esquema;
    /// luego se crea la pivote <c>CapacitacionResponsable</c>. Los datos previos (responsables
    /// inline por capacitación) se pierden — aprobado explícitamente en Task #5.
    /// </summary>
    public partial class RefactorResponsablesCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop de la tabla Responsable vieja (hija de Capacitacion).
            //    EF la creó con FK a Capacitacion + índice único (CapacitacionId, Orden).
            //    DropTable limpia todo lo dependiente en un solo paso.
            migrationBuilder.DropTable(
                name: "Responsable",
                schema: "dbo");

            // 2. Recrear Responsable con el nuevo esquema de catálogo global.
            migrationBuilder.CreateTable(
                name: "Responsable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Empresa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Firma = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responsable", x => x.Id);
                });

            // 3. Crear la pivote N–N CapacitacionResponsable.
            //    - PK compuesta (CapacitacionId, ResponsableId)
            //    - Índice único (CapacitacionId, Orden) para orden único por capacitación.
            //    - FK a Capacitacion → Cascade (al borrar físicamente una capacitación se limpia la pivote).
            //    - FK a Responsable → Restrict (no se puede borrar físicamente un responsable referenciado;
            //      el admin usa baja lógica vía Activo = false).
            migrationBuilder.CreateTable(
                name: "CapacitacionResponsable",
                schema: "dbo",
                columns: table => new
                {
                    CapacitacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacitacionResponsable", x => new { x.CapacitacionId, x.ResponsableId });
                    table.ForeignKey(
                        name: "FK_CapacitacionResponsable_Capacitacion_CapacitacionId",
                        column: x => x.CapacitacionId,
                        principalSchema: "dbo",
                        principalTable: "Capacitacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CapacitacionResponsable_Responsable_ResponsableId",
                        column: x => x.ResponsableId,
                        principalSchema: "dbo",
                        principalTable: "Responsable",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapacitacionResponsable_ResponsableId",
                schema: "dbo",
                table: "CapacitacionResponsable",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "UX_CapacitacionResponsable_Capacitacion_Orden",
                schema: "dbo",
                table: "CapacitacionResponsable",
                columns: new[] { "CapacitacionId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapacitacionResponsable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Responsable",
                schema: "dbo");

            // Recrear el esquema viejo (Responsable como hijo de Capacitacion).
            // Útil si alguien ejecuta Update-Database -TargetMigration a una versión anterior.
            migrationBuilder.CreateTable(
                name: "Responsable",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapacitacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombres = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Empresa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Firma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Responsable", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Responsable_Capacitacion_CapacitacionId",
                        column: x => x.CapacitacionId,
                        principalSchema: "dbo",
                        principalTable: "Capacitacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Responsable_Capacitacion_Orden",
                schema: "dbo",
                table: "Responsable",
                columns: new[] { "CapacitacionId", "Orden" },
                unique: true);
        }
    }
}
