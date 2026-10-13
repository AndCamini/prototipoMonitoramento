using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Persistencia.Mapeamentos;

// Tabela 5 — dicionário de dados da tabela servico_tcp.
internal sealed class ServicoTcpMap : IEntityTypeConfiguration<ServicoTcp>
{
    public void Configure(EntityTypeBuilder<ServicoTcp> b)
    {
        b.ToTable("servico_tcp", t =>
        {
            t.HasCheckConstraint("ck_servico_tcp_porta", "porta BETWEEN 1 AND 65535");
            t.HasCheckConstraint("ck_servico_tcp_status", "status IN (0, 1, 2)");
        });

        b.HasKey(s => s.Id).HasName("pk_servico_tcp");
        b.Property(s => s.Id).HasColumnName("id").HasColumnOrder(0).ValueGeneratedNever();
        b.Property(s => s.DispositivoId).HasColumnName("dispositivo_id").HasColumnOrder(1);
        b.Property(s => s.Nome).HasColumnName("nome").HasColumnOrder(2).HasMaxLength(120).IsRequired();
        b.Property(s => s.Porta).HasColumnName("porta").HasColumnOrder(3).HasField("_porta");
        b.Property(s => s.Ativo).HasColumnName("ativo").HasColumnOrder(4).HasDefaultValue(true).ValueGeneratedNever();
        b.Property(s => s.Status).HasColumnName("status").HasColumnOrder(5).HasDefaultValue(StatusAtivo.Desconhecido).ValueGeneratedNever();
        b.Property(s => s.TentativasSemResposta).HasColumnName("tentativas_sem_resposta").HasColumnOrder(6)
            .HasConversion<short>().HasColumnType("smallint").HasDefaultValue(0).ValueGeneratedNever();

        b.Ignore(s => s.StatusAnterior);
        b.Ignore(s => s.EstaVerificando);
        b.Ignore(s => s.DescricaoStatus);

        b.HasIndex(s => new { s.DispositivoId, s.Porta }).IsUnique().HasDatabaseName("uq_servico_tcp_porta");
        b.HasIndex(s => s.DispositivoId).HasDatabaseName("ix_servico_tcp_dispositivo");
    }
}
