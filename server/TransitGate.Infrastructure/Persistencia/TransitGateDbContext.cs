using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TransitGate.Application;
using TransitGate.Domain.Accesos;
using TransitGate.Domain.Alertas;
using TransitGate.Domain.Clientes;
using TransitGate.Domain.Tarjetas;
using TransitGate.Domain.Torniquetes;

namespace TransitGate.Infrastructure.Persistencia;

/// <summary>
/// La base de datos (PostgreSQL). Es también la unidad de trabajo: un caso de uso termina con un solo
/// SaveChanges, así el débito, el paso y el intento se guardan juntos o no se guarda ninguno (RNF-15).
/// El Torniquete no está aquí: su estado vive en memoria (TorniquetesEnMemoria).
/// </summary>
public class TransitGateDbContext(DbContextOptions<TransitGateDbContext> opciones)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(opciones), IUnidadDeTrabajo
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Tarjeta> Tarjetas => Set<Tarjeta>();
    public DbSet<SoporteTarjeta> Soportes => Set<SoporteTarjeta>();
    public DbSet<Recarga> Recargas => Set<Recarga>();
    public DbSet<DebitoPaso> Debitos => Set<DebitoPaso>();
    public DbSet<IntentoAcceso> Intentos => Set<IntentoAcceso>();
    public DbSet<Cruce> Cruces => Set<Cruce>();
    public DbSet<AlertaSeguridad> Alertas => Set<AlertaSeguridad>();

    /// <summary>
    /// Un solo SaveChanges. Si el caso de uso abrió una transacción (por ejemplo, el registro, porque Identity
    /// guarda por su cuenta), aquí se confirma; si no se llega aquí, se deshace al terminar la petición.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken ct = default)
    {
        await SaveChangesAsync(ct);
        if (Database.CurrentTransaction is { } transaccion)
            await transaccion.CommitAsync(ct);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configuracion)
    {
        // Los enums se guardan con su nombre, para leer la base sin una tabla de equivalencias.
        configuracion.Properties<EstadoSoporte>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<TipoSoporte>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<MotivoBloqueo>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<ResultadoIntento>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<MotivoIntento>().HaveConversion<string>().HaveMaxLength(30);
        configuracion.Properties<EstadoAlerta>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<SentidoCruce>().HaveConversion<string>().HaveMaxLength(20);
        configuracion.Properties<TipoResultadoCruce>().HaveConversion<string>().HaveMaxLength(20);

        // Dinero en pesos: entero en el JSON (RNF-24), decimal adentro.
        configuracion.Properties<decimal>().HavePrecision(12, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);

        foreach (var secuencia in Secuencias.Todas)
            modelo.HasSequence<long>(secuencia);

        modelo.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes");
            e.Property(c => c.NombreCompleto).HasMaxLength(120);
            e.Property(c => c.TipoDocumento).HasMaxLength(2);
            e.Property(c => c.NumeroDocumento).HasMaxLength(12);
            e.Property(c => c.Correo).HasMaxLength(254);
            e.Property(c => c.Telefono).HasMaxLength(10);
            e.HasIndex(c => c.Correo).IsUnique();             // RF-01 CA2
            e.HasIndex(c => c.NumeroDocumento).IsUnique();
        });

        modelo.Entity<Tarjeta>(e =>
        {
            e.ToTable("Tarjetas");
            e.HasOne<Cliente>().WithOne().HasForeignKey<Tarjeta>(t => t.ClienteId);   // una por cliente (RF-07)
            // xmin de PostgreSQL como control de concurrencia del saldo (RNF-16).
            e.Property<uint>("Version").IsRowVersion();
        });

        modelo.Entity<SoporteTarjeta>(e =>
        {
            e.ToTable("Soportes");
            e.HasOne<Tarjeta>().WithMany().HasForeignKey(s => s.TarjetaId);
            e.Property(s => s.Uid).HasMaxLength(14);
            e.HasIndex(s => s.Uid).IsUnique();                 // un UID, un soporte (RF-10 CA2)
            e.HasIndex(s => s.Estado);
            e.Ignore(s => s.Vigente);
        });

        modelo.Entity<Recarga>(e =>
        {
            e.ToTable("Recargas");
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasOne<Tarjeta>().WithMany().HasForeignKey(r => r.TarjetaId);
            e.Property(r => r.ClaveIdempotencia).HasMaxLength(64);
            e.HasIndex(r => new { r.TarjetaId, r.ClaveIdempotencia }).IsUnique();   // RF-15 CA5
        });

        modelo.Entity<DebitoPaso>(e =>
        {
            e.ToTable("DebitosPaso");
            e.HasKey(d => d.PasoId);
            e.Property(d => d.PasoId).ValueGeneratedNever();
            e.HasOne<Tarjeta>().WithMany().HasForeignKey(d => d.TarjetaId);
        });

        modelo.Entity<IntentoAcceso>(e =>
        {
            e.ToTable("IntentosAcceso");
            e.Property(i => i.Id).ValueGeneratedNever();
            e.Property(i => i.TorniqueteId).HasMaxLength(20);
            e.Property(i => i.LecturaId).HasMaxLength(40);
            e.Property(i => i.UidLeido).HasMaxLength(14);
            e.HasIndex(i => new { i.TorniqueteId, i.LecturaId }).IsUnique();   // RF-18 CA4
            e.HasIndex(i => i.TarjetaId);                                       // RF-39
        });

        modelo.Entity<Cruce>(e =>
        {
            e.ToTable("Cruces");
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.TorniqueteId).HasMaxLength(20);
            e.Property(c => c.SesionVision).HasMaxLength(64);
            e.HasIndex(c => new { c.TorniqueteId, c.SesionVision, c.TrackId }).IsUnique();   // duplicado
        });

        modelo.Entity<AlertaSeguridad>(e =>
        {
            e.ToTable("AlertasSeguridad");
            e.Property(a => a.Id).ValueGeneratedNever();
            e.Property(a => a.TorniqueteId).HasMaxLength(20);
            e.HasIndex(a => new { a.TorniqueteId, a.Estado });
        });

        modelo.Entity<Usuario>(e => e.Property(u => u.NombreCompleto).HasMaxLength(120));
    }
}
