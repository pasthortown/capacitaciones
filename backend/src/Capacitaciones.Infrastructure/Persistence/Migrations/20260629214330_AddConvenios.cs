using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConvenios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Convenio",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CedulaColaborador = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NombreColaborador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OrigenColaborador = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TipoCurso = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NombreCurso = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Marca = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MesesADevengar = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaCorte = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MontoCongelado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Convenio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConvenioAnexo",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConvenioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreOriginal = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NombreAlmacenado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TamanoBytes = table.Column<long>(type: "bigint", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvenioAnexo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvenioAnexo_Convenio_ConvenioId",
                        column: x => x.ConvenioId,
                        principalSchema: "dbo",
                        principalTable: "Convenio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConvenioItem",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConvenioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Devengable = table.Column<bool>(type: "bit", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvenioItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvenioItem_Convenio_ConvenioId",
                        column: x => x.ConvenioId,
                        principalSchema: "dbo",
                        principalTable: "Convenio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Convenio_Cedula",
                schema: "dbo",
                table: "Convenio",
                column: "CedulaColaborador");

            migrationBuilder.CreateIndex(
                name: "IX_ConvenioAnexo_ConvenioId",
                schema: "dbo",
                table: "ConvenioAnexo",
                column: "ConvenioId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvenioItem_ConvenioId",
                schema: "dbo",
                table: "ConvenioItem",
                column: "ConvenioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConvenioAnexo",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ConvenioItem",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Convenio",
                schema: "dbo");
        }
    }
}
