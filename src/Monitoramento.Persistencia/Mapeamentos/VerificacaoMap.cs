using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Persistencia.Mapeamentos;

// Tabela 6 — dicionário de dados da tabela verificacao.
internal sealed class VerificacaoMap : IEntityTypeConfiguration<Verificacao>
{
    public void Configure(EntityTypeBuilder<Verificacao> b)
    {
        b.ToTable("verificacao", t =>
        {
            t.HasCheckConstraint("ck_verificacao_tipo", "tipo IN (0, 1)");
            t.HasCheckConstraint("ck_verificacao_latencia", "latencia_ms IS NULL OR latencia_ms >= 0");
        });

        b.HasKey(v => v.Id).HasName("pk_verificacao");
        b.Property(v => v.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(v => v.DispositivoId).HasColumnName("dispositivo_id");
        b.Property(v => v.ServicoTcpId).HasColumnName("servico_tcp_id");
        b.Property(v => v.DataHora).HasColumnName("data_hora");
        b.Property(v => v.Tipo).HasColumnName("tipo");
        b.Property(v => v.Sucesso).HasColumnName("sucesso");
        b.Property(v => v.LatenciaMs).HasColumnName("latencia_ms");
        b.Property(v => v.MensagemErro).HasColumnName("mensagem_erro").HasMaxLength(Verificacao.TamanhoMaximoMensagem);

        b.HasOne<Dispositivo>().WithMany().HasForeignKey(v => v.DispositivoId)
            .HasConstraintName("fk_verificacao_dispositivo").OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ServicoTcp>().WithMany().HasForeignKey(v => v.ServicoTcpId)
            .HasConstraintName("fk_verificacao_servico").OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(v => new { v.DispositivoId, v.DataHora })
            .IsDescending(false, true)
            .HasDatabaseName("ix_verificacao_dispositivo_data");
        b.HasIndex(v => new { v.ServicoTcpId, v.DataHora })
            .IsDescending(false, true)
            .HasFilter("servico_tcp_id IS NOT NULL")
            .HasDatabaseName("ix_verificacao_servico_data");
    }
}
