using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCapacitaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Capacitacion",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tema = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Capacitador = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CargoCapacitador = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    EmpresaCapacitador = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    FirmaCapacitador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModalidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoActividadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCertificacion = table.Column<int>(type: "int", nullable: false),
                    FechaHoraInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuracionMinutos = table.Column<int>(type: "int", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Capacitacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Capacitacion_Modalidad_ModalidadId",
                        column: x => x.ModalidadId,
                        principalSchema: "dbo",
                        principalTable: "Modalidad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Capacitacion_TipoActividad_TipoActividadId",
                        column: x => x.TipoActividadId,
                        principalSchema: "dbo",
                        principalTable: "TipoActividad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_Capacitacion_ModalidadId",
                schema: "dbo",
                table: "Capacitacion",
                column: "ModalidadId");

            migrationBuilder.CreateIndex(
                name: "IX_Capacitacion_TipoActividadId",
                schema: "dbo",
                table: "Capacitacion",
                column: "TipoActividadId");

            migrationBuilder.CreateIndex(
                name: "UX_Capacitacion_Codigo",
                schema: "dbo",
                table: "Capacitacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Responsable_Capacitacion_Orden",
                schema: "dbo",
                table: "Responsable",
                columns: new[] { "CapacitacionId", "Orden" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Responsable",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Capacitacion",
                schema: "dbo");
        }
    }
}
