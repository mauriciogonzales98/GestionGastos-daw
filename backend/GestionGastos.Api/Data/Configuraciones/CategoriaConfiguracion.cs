using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionGastos.Api.Data.Configuraciones;

public sealed class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> constructor)
    {
        constructor.ToTable("categorias");
        constructor.HasKey(c => c.Id);

        constructor.Property(c => c.Id).HasColumnName("id").ValueGeneratedOnAdd();
        constructor.Property(c => c.Nombre).HasColumnName("nombre").HasMaxLength(60).IsRequired();
        constructor.Property(c => c.Tipo)
            .HasColumnName("tipo")
            .HasConversion<byte>()
            .HasColumnType("tinyint unsigned")
            .IsRequired();

        // "Otros" existe en los dos tipos: la unicidad es del par, no del nombre.
        constructor.HasIndex(c => new { c.Nombre, c.Tipo })
            .IsUnique()
            .HasDatabaseName("ux_categorias_nombre_tipo");

        constructor.HasData(
            new Categoria { Id = 1, Nombre = "Comida", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 2, Nombre = "Transporte", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 3, Nombre = "Vivienda", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 4, Nombre = "Servicios", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 5, Nombre = "Salud", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 6, Nombre = "Ocio", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 7, Nombre = "Otros", Tipo = TipoMovimiento.Gasto },
            new Categoria { Id = 8, Nombre = "Sueldo", Tipo = TipoMovimiento.Ingreso },
            new Categoria { Id = 9, Nombre = "Ingreso extra", Tipo = TipoMovimiento.Ingreso },
            new Categoria { Id = 10, Nombre = "Otros", Tipo = TipoMovimiento.Ingreso });
    }
}
