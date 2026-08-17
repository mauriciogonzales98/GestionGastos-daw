using GestionGastos.Api.Common;
using GestionGastos.Api.Data.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestionGastos.Api.Data.Configuraciones;

public sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    /// <summary>Fecha fija: <c>HasData</c> exige un valor constante entre migraciones.</summary>
    private static readonly DateTime CreadoEnSemilla =
        new(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Usuario> constructor)
    {
        constructor.ToTable("usuarios");
        constructor.HasKey(u => u.Id);

        constructor.Property(u => u.Id).HasColumnName("id").ValueGeneratedOnAdd();
        constructor.Property(u => u.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        constructor.Property(u => u.CreadoEn).HasColumnName("creado_en").HasColumnType("datetime(6)").IsRequired();

        constructor.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_usuarios_email");

        constructor.HasData(new Usuario
        {
            Id = 1,
            Email = UsuarioSemillaActual.EmailSemilla,
            CreadoEn = CreadoEnSemilla,
        });
    }
}
