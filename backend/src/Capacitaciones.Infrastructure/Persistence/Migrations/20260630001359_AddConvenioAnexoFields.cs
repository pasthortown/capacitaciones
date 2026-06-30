using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvenioAnexoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CentroCostos",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ConvenioFirmado",
                schema: "dbo",
                table: "Convenio",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmpresaColaborador",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinCurso",
                schema: "dbo",
                table: "Convenio",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFirma",
                schema: "dbo",
                table: "Convenio",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaIngreso",
                schema: "dbo",
                table: "Convenio",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioCurso",
                schema: "dbo",
                table: "Convenio",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Horas",
                schema: "dbo",
                table: "Convenio",
                type: "decimal(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JefeInmediato",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroRegistro",
                schema: "dbo",
                table: "Convenio",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelacionLaboral",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resultado",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAsumidoEmpresa",
                schema: "dbo",
                table: "Convenio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ConvenioNumeracion",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SiguienteNumero = table.Column<int>(type: "int", nullable: false),
                    UltimaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvenioNumeracion", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "ConvenioNumeracion",
                columns: new[] { "Id", "SiguienteNumero", "UltimaActualizacion" },
                values: new object[] { 1, 1, null });

            // Backfill convenios existentes:
            // 1) Preservar el devengo legado: ValorAsumidoEmpresa = suma de ítems devengables
            //    (antes la base era esa suma; ahora es este campo explícito).
            migrationBuilder.Sql(@"
UPDATE c
SET c.ValorAsumidoEmpresa = ISNULL((
    SELECT SUM(i.Valor) FROM dbo.ConvenioItem i
    WHERE i.ConvenioId = c.Id AND i.Devengable = 1), 0)
FROM dbo.Convenio c;");

            // 2) Asignar número de registro secuencial por orden de creación.
            migrationBuilder.Sql(@"
;WITH ordered AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY FechaCreacion, Id) AS rn FROM dbo.Convenio
)
UPDATE c SET c.NumeroRegistro = o.rn
FROM dbo.Convenio c JOIN ordered o ON c.Id = o.Id;");

            // 3) Avanzar el contador más allá del máximo asignado.
            migrationBuilder.Sql(@"
UPDATE dbo.ConvenioNumeracion
SET SiguienteNumero = (SELECT ISNULL(MAX(NumeroRegistro), 0) + 1 FROM dbo.Convenio)
WHERE Id = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConvenioNumeracion",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "CentroCostos",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "ConvenioFirmado",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "EmpresaColaborador",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "FechaFinCurso",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "FechaFirma",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "FechaIngreso",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "FechaInicioCurso",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "Horas",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "JefeInmediato",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "NumeroRegistro",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "RelacionLaboral",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "Resultado",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "ValorAsumidoEmpresa",
                schema: "dbo",
                table: "Convenio");
        }
    }
}
