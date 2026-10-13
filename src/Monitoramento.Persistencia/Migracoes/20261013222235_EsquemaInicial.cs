using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Monitoramento.Persistencia.Migracoes
{
    /// <inheritdoc />
    public partial class EsquemaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispositivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    endereco = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    intervalo_segundos = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    limite_tentativas = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)3),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    ultima_verificacao_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tentativas_sem_resposta = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispositivo", x => x.id);
                    table.CheckConstraint("ck_dispositivo_intervalo", "intervalo_segundos BETWEEN 10 AND 3600");
                    table.CheckConstraint("ck_dispositivo_status", "status IN (0, 1, 2)");
                    table.CheckConstraint("ck_dispositivo_tentativas", "limite_tentativas BETWEEN 1 AND 10");
                });

            migrationBuilder.CreateTable(
                name: "servico_tcp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    porta = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    tentativas_sem_resposta = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_servico_tcp", x => x.id);
                    table.CheckConstraint("ck_servico_tcp_porta", "porta BETWEEN 1 AND 65535");
                    table.CheckConstraint("ck_servico_tcp_status", "status IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "fk_servico_tcp_dispositivo",
                        column: x => x.dispositivo_id,
                        principalTable: "dispositivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alerta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_tcp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    iniciado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    encerrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerta", x => x.id);
                    table.CheckConstraint("ck_alerta_periodo", "encerrado_em IS NULL OR encerrado_em >= iniciado_em");
                    table.CheckConstraint("ck_alerta_tipo", "tipo IN (0, 1)");
                    table.ForeignKey(
                        name: "fk_alerta_dispositivo",
                        column: x => x.dispositivo_id,
                        principalTable: "dispositivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_alerta_servico",
                        column: x => x.servico_tcp_id,
                        principalTable: "servico_tcp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verificacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispositivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_tcp_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_hora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    sucesso = table.Column<bool>(type: "boolean", nullable: false),
                    latencia_ms = table.Column<int>(type: "integer", nullable: true),
                    mensagem_erro = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_verificacao", x => x.id);
                    table.CheckConstraint("ck_verificacao_latencia", "latencia_ms IS NULL OR latencia_ms >= 0");
                    table.CheckConstraint("ck_verificacao_tipo", "tipo IN (0, 1)");
                    table.ForeignKey(
                        name: "fk_verificacao_dispositivo",
                        column: x => x.dispositivo_id,
                        principalTable: "dispositivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_verificacao_servico",
                        column: x => x.servico_tcp_id,
                        principalTable: "servico_tcp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerta_aberto",
                table: "alerta",
                columns: new[] { "dispositivo_id", "iniciado_em" },
                descending: new[] { false, true },
                filter: "encerrado_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_servico",
                table: "alerta",
                column: "servico_tcp_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispositivo_ativo",
                table: "dispositivo",
                column: "ativo",
                filter: "ativo = TRUE");

            migrationBuilder.CreateIndex(
                name: "uq_dispositivo_endereco",
                table: "dispositivo",
                column: "endereco",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_servico_tcp_dispositivo",
                table: "servico_tcp",
                column: "dispositivo_id");

            migrationBuilder.CreateIndex(
                name: "uq_servico_tcp_porta",
                table: "servico_tcp",
                columns: new[] { "dispositivo_id", "porta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verificacao_dispositivo_data",
                table: "verificacao",
                columns: new[] { "dispositivo_id", "data_hora" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_verificacao_servico_data",
                table: "verificacao",
                columns: new[] { "servico_tcp_id", "data_hora" },
                descending: new[] { false, true },
                filter: "servico_tcp_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta");

            migrationBuilder.DropTable(
                name: "verificacao");

            migrationBuilder.DropTable(
                name: "servico_tcp");

            migrationBuilder.DropTable(
                name: "dispositivo");
        }
    }
}
