using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Capacitaciones.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalogos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Area",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Area", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Modalidad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modalidad", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TipoActividad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    FechaActualizacion = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoActividad", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Modalidad",
                columns: new[] { "Id", "Activo", "FechaActualizacion", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111001"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Presencial" },
                    { new Guid("11111111-1111-1111-1111-111111111002"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Virtual" },
                    { new Guid("11111111-1111-1111-1111-111111111003"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Híbrida" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TipoActividad",
                columns: new[] { "Id", "Activo", "FechaActualizacion", "FechaCreacion", "Nombre" },
                values: new object[,]
                {
                    { new Guid("22222222-2222-2222-2222-222222222001"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Charla" },
                    { new Guid("22222222-2222-2222-2222-222222222002"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Workshop" },
                    { new Guid("22222222-2222-2222-2222-222222222003"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Capacitación" },
                    { new Guid("22222222-2222-2222-2222-222222222004"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Curso" },
                    { new Guid("22222222-2222-2222-2222-222222222005"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Taller" },
                    { new Guid("22222222-2222-2222-2222-222222222006"), true, null, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Seminario" }
                });

            migrationBuilder.CreateIndex(
                name: "UX_Area_Nombre",
                schema: "dbo",
                table: "Area",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Modalidad_Nombre",
                schema: "dbo",
                table: "Modalidad",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_TipoActividad_Nombre",
                schema: "dbo",
                table: "TipoActividad",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Area",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Modalidad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoActividad",
                schema: "dbo");
        }
    }
}
