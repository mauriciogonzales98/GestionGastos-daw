using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionGastos.Api.Data.Configuraciones;

public sealed class MovimientoConfiguracion : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> constructor)
    {
        constructor.ToTable("movimientos");
        constructor.HasKey(m => m.Id);

        constructor.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
        constructor.Property(m => m.UsuarioId).HasColumnName("usuario_id").IsRequired();
        constructor.Property(m => m.CategoriaId).HasColumnName("categoria_id").IsRequired();

        constructor.Property(m => m.Tipo)
            .HasColumnName("tipo")
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned")
            .IsRequired();

        // Explícito a propósito: el default de Pomelo para decimal es decimal(65,30), que
        // incumpliría NFR-03 sin que ningún test de suma lo note.
        constructor.Property(m => m.Monto)
            .HasColumnName("monto")
            .HasPrecision(15, 2)
            .IsRequired();

        constructor.Property(m => m.Moneda)
            .HasColumnName("moneda")
            .HasColumnType($"char({Common.Moneda.Largo})")
            .HasDefaultValue(Common.Moneda.Predeterminada)
            .IsRequired();

        constructor.Property(m => m.Fecha)
            .HasColumnName("fecha")
            .HasColumnType("date")
            .IsRequired();

        constructor.Property(m => m.Nota).HasColumnName("nota").HasMaxLength(120);

        constructor.Property(m => m.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("datetime(6)")
            .IsRequired();

        constructor.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimientos_usuario");

        constructor.HasOne(m => m.Categoria)
            .WithMany()
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_movimientos_categoria");

        // Ordena el listado del propietario por fecha e id descendentes sin pasar por filesort.
        constructor.HasIndex(m => new { m.UsuarioId, m.Fecha, m.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_movimientos_usuario_fecha_id");
    }
}
