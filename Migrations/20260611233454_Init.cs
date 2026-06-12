using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PichangasApi.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    TallaCm = table.Column<int>(type: "integer", nullable: true),
                    Posiciones = table.Column<string[]>(type: "text[]", nullable: false),
                    Evaluado = table.Column<bool>(type: "boolean", nullable: false),
                    Saque = table.Column<int>(type: "integer", nullable: false),
                    Ataque = table.Column<int>(type: "integer", nullable: false),
                    Bloqueo = table.Column<int>(type: "integer", nullable: false),
                    Recepcion = table.Column<int>(type: "integer", nullable: false),
                    Defensa = table.Column<int>(type: "integer", nullable: false),
                    Colocacion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pichangas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Distrito = table.Column<string>(type: "text", nullable: false),
                    Lat = table.Column<double>(type: "double precision", nullable: true),
                    Lng = table.Column<double>(type: "double precision", nullable: true),
                    Publica = table.Column<bool>(type: "boolean", nullable: false),
                    AdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    Capacidad = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pichangas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pichangas_Usuarios_AdminId",
                        column: x => x.AdminId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Validaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmigoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Saque = table.Column<int>(type: "integer", nullable: false),
                    Ataque = table.Column<int>(type: "integer", nullable: false),
                    Bloqueo = table.Column<int>(type: "integer", nullable: false),
                    Recepcion = table.Column<int>(type: "integer", nullable: false),
                    Defensa = table.Column<int>(type: "integer", nullable: false),
                    Colocacion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Validaciones_Usuarios_AmigoId",
                        column: x => x.AmigoId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Validaciones_Usuarios_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PichangaMiembros",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PichangaId = table.Column<Guid>(type: "uuid", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PichangaMiembros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PichangaMiembros_Pichangas_PichangaId",
                        column: x => x.PichangaId,
                        principalTable: "Pichangas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PichangaMiembros_Usuarios_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SolicitudesUnion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PichangaId = table.Column<Guid>(type: "uuid", nullable: false),
                    JugadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesUnion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolicitudesUnion_Pichangas_PichangaId",
                        column: x => x.PichangaId,
                        principalTable: "Pichangas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SolicitudesUnion_Usuarios_JugadorId",
                        column: x => x.JugadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PichangaMiembros_JugadorId",
                table: "PichangaMiembros",
                column: "JugadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PichangaMiembros_PichangaId",
                table: "PichangaMiembros",
                column: "PichangaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pichangas_AdminId",
                table: "Pichangas",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesUnion_JugadorId",
                table: "SolicitudesUnion",
                column: "JugadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesUnion_PichangaId",
                table: "SolicitudesUnion",
                column: "PichangaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Validaciones_AmigoId",
                table: "Validaciones",
                column: "AmigoId");

            migrationBuilder.CreateIndex(
                name: "IX_Validaciones_JugadorId",
                table: "Validaciones",
                column: "JugadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PichangaMiembros");

            migrationBuilder.DropTable(
                name: "SolicitudesUnion");

            migrationBuilder.DropTable(
                name: "Validaciones");

            migrationBuilder.DropTable(
                name: "Pichangas");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
