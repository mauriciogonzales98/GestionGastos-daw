using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GestionGastos.Api.Migrations
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    nombre = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tipo = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorias", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    email = table.Column<string>(type: "varchar(320)", maxLength: 320, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "movimientos",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    usuario_id = table.Column<int>(type: "int", nullable: false),
                    tipo = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    categoria_id = table.Column<int>(type: "int", nullable: false),
                    monto = table.Column<decimal>(type: "decimal(15,2)", precision: 15, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "ARS")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    nota = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    creado_en = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movimientos", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimientos_categoria",
                        column: x => x.categoria_id,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimientos_usuario",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "categorias",
                columns: new[] { "id", "nombre", "tipo" },
                values: new object[,]
                {
                    { 1, "Comida", (byte)1 },
                    { 2, "Transporte", (byte)1 },
                    { 3, "Vivienda", (byte)1 },
                    { 4, "Servicios", (byte)1 },
                    { 5, "Salud", (byte)1 },
                    { 6, "Ocio", (byte)1 },
                    { 7, "Otros", (byte)1 },
                    { 8, "Sueldo", (byte)2 },
                    { 9, "Ingreso extra", (byte)2 },
                    { 10, "Otros", (byte)2 }
                });

            migrationBuilder.InsertData(
                table: "usuarios",
                columns: new[] { "id", "creado_en", "email" },
                values: new object[] { 1, new DateTime(2026, 8, 17, 0, 0, 0, 0, DateTimeKind.Utc), "dev@gestiongastos.local" });

            migrationBuilder.CreateIndex(
                name: "ux_categorias_nombre_tipo",
                table: "categorias",
                columns: new[] { "nombre", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_movimientos_categoria_id",
                table: "movimientos",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimientos_usuario_fecha_id",
                table: "movimientos",
                columns: new[] { "usuario_id", "fecha", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_usuarios_email",
                table: "usuarios",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "movimientos");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
