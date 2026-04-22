using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCapacitador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailCapacitador",
                schema: "dbo",
                table: "Capacitacion",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailCapacitador",
                schema: "dbo",
                table: "Capacitacion");
        }
    }
}
