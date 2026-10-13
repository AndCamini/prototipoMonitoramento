using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Persistencia.Mapeamentos;

// Tabela 7 — dicionário de dados da tabela alerta.
internal sealed class AlertaMap : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("alerta", t =>
        {
            t.HasCheckConstraint("ck_alerta_tipo", "tipo IN (0, 1)");
            t.HasCheckConstraint("ck_alerta_periodo", "encerrado_em IS NULL OR encerrado_em >= iniciado_em");
        });

        b.HasKey(a => a.Id).HasName("pk_alerta");
        b.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(a => a.DispositivoId).HasColumnName("dispositivo_id");
        b.Property(a => a.ServicoTcpId).HasColumnName("servico_tcp_id");
        b.Property(a => a.Tipo).HasColumnName("tipo");
        b.Property(a => a.IniciadoEm).HasColumnName("iniciado_em");
        b.Property(a => a.EncerradoEm).HasColumnName("encerrado_em");
        b.Property(a => a.Descricao).HasColumnName("descricao").HasMaxLength(Alerta.TamanhoMaximoDescricao);

        b.HasOne<Dispositivo>().WithMany().HasForeignKey(a => a.DispositivoId)
            .HasConstraintName("fk_alerta_dispositivo").OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ServicoTcp>().WithMany().HasForeignKey(a => a.ServicoTcpId)
            .HasConstraintName("fk_alerta_servico").OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(a => new { a.DispositivoId, a.IniciadoEm })
            .IsDescending(false, true)
            .HasFilter("encerrado_em IS NULL")
            .HasDatabaseName("ix_alerta_aberto");
        b.HasIndex(a => a.ServicoTcpId).HasDatabaseName("ix_alerta_servico");
    }
}
