using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvenioQuitarTituloAgregarReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Titulo",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.AddColumn<Guid>(
                name: "ConvenioReferenciaId",
                schema: "dbo",
                table: "Convenio",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Convenio_Referencia",
                schema: "dbo",
                table: "Convenio",
                column: "ConvenioReferenciaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Convenio_Convenio_ConvenioReferenciaId",
                schema: "dbo",
                table: "Convenio",
                column: "ConvenioReferenciaId",
                principalSchema: "dbo",
                principalTable: "Convenio",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Convenio_Convenio_ConvenioReferenciaId",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropIndex(
                name: "IX_Convenio_Referencia",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.DropColumn(
                name: "ConvenioReferenciaId",
                schema: "dbo",
                table: "Convenio");

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");
        }
    }
}
