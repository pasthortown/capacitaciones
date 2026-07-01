using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvenioQuitarTipoCurso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoCurso",
                schema: "dbo",
                table: "Convenio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoCurso",
                schema: "dbo",
                table: "Convenio",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }
    }
}
