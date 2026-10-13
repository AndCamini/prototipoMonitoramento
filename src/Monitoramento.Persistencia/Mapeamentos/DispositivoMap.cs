using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Persistencia.Mapeamentos;

// Tabela 4 — dicionário de dados da tabela dispositivo.
internal sealed class DispositivoMap : IEntityTypeConfiguration<Dispositivo>
{
    public void Configure(EntityTypeBuilder<Dispositivo> b)
    {
        b.ToTable("dispositivo", t =>
        {
            t.HasCheckConstraint("ck_dispositivo_intervalo", "intervalo_segundos BETWEEN 10 AND 3600");
            t.HasCheckConstraint("ck_dispositivo_tentativas", "limite_tentativas BETWEEN 1 AND 10");
            t.HasCheckConstraint("ck_dispositivo_status", "status IN (0, 1, 2)");
        });

        b.HasKey(d => d.Id).HasName("pk_dispositivo");
        b.Property(d => d.Id).HasColumnName("id").HasColumnOrder(0).ValueGeneratedNever();
        b.Property(d => d.Nome).HasColumnName("nome").HasColumnOrder(1).HasMaxLength(120).IsRequired();
        b.Property(d => d.Endereco).HasColumnName("endereco").HasColumnOrder(2).HasMaxLength(255).IsRequired();
        b.Property(d => d.Descricao).HasColumnName("descricao").HasColumnOrder(3).HasMaxLength(500);
        // Os valores padrão documentam o esquema; o EF sempre envia o valor da entidade.
        b.Property(d => d.Ativo).HasColumnName("ativo").HasColumnOrder(4).HasDefaultValue(true).ValueGeneratedNever();
        b.Property(d => d.IntervaloSegundos).HasColumnName("intervalo_segundos").HasColumnOrder(5)
            .HasField("_intervaloSegundos").HasDefaultValue(60).ValueGeneratedNever();
        b.Property(d => d.LimiteTentativas).HasColumnName("limite_tentativas").HasColumnOrder(6)
            .HasField("_limiteTentativas").HasConversion<short>().HasColumnType("smallint")
            .HasDefaultValue(3).ValueGeneratedNever();
        b.Property(d => d.Status).HasColumnName("status").HasColumnOrder(7).HasDefaultValue(StatusAtivo.Desconhecido).ValueGeneratedNever();
        b.Property(d => d.UltimaVerificacaoEm).HasColumnName("ultima_verificacao_em").HasColumnOrder(8);
        b.Property(d => d.TentativasSemResposta).HasColumnName("tentativas_sem_resposta").HasColumnOrder(9)
            .HasConversion<short>().HasColumnType("smallint").HasDefaultValue(0).ValueGeneratedNever();
        b.Property(d => d.CriadoEm).HasColumnName("criado_em").HasColumnOrder(10).HasDefaultValueSql("now()").ValueGeneratedNever();

        b.Ignore(d => d.StatusAnterior);
        b.Ignore(d => d.EstaVerificando);
        b.Ignore(d => d.DescricaoStatus);

        b.HasMany(d => d.Servicos).WithOne(s => s.Dispositivo)
            .HasForeignKey(s => s.DispositivoId)
            .HasConstraintName("fk_servico_tcp_dispositivo")
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(d => d.Servicos).HasField("_servicos").UsePropertyAccessMode(PropertyAccessMode.Field);

        b.HasIndex(d => d.Endereco).IsUnique().HasDatabaseName("uq_dispositivo_endereco");
        b.HasIndex(d => d.Ativo).HasDatabaseName("ix_dispositivo_ativo").HasFilter("ativo = TRUE");
    }
}
