using Microsoft.EntityFrameworkCore;
using PichangasApi.Models;

namespace PichangasApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Validacion> Validaciones => Set<Validacion>();
    public DbSet<Pichanga> Pichangas => Set<Pichanga>();
    public DbSet<PichangaMiembro> PichangaMiembros => Set<PichangaMiembro>();
    public DbSet<SolicitudUnion> SolicitudesUnion => Set<SolicitudUnion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Usuario>(e =>
            e.HasIndex(u => u.Email).IsUnique());

        modelBuilder.Entity<Validacion>(e =>
        {
            e.HasOne(v => v.Jugador)
                .WithMany(u => u.ValidacionesRecibidas)
                .HasForeignKey(v => v.JugadorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.Amigo)
                .WithMany(u => u.ValidacionesDadas)
                .HasForeignKey(v => v.AmigoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pichanga>(e =>
            e.HasOne(p => p.Admin)
                .WithMany(u => u.PichangasAdministradas)
                .HasForeignKey(p => p.AdminId)
                .OnDelete(DeleteBehavior.Restrict));

        modelBuilder.Entity<PichangaMiembro>(e =>
        {
            e.HasOne(m => m.Pichanga)
                .WithMany(p => p.Miembros)
                .HasForeignKey(m => m.PichangaId);
            e.HasOne(m => m.Jugador)
                .WithMany(u => u.Membresias)
                .HasForeignKey(m => m.JugadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SolicitudUnion>(e =>
        {
            e.Property(s => s.Estado).HasConversion<string>();
            e.HasOne(s => s.Pichanga)
                .WithMany(p => p.Solicitudes)
                .HasForeignKey(s => s.PichangaId);
            e.HasOne(s => s.Jugador)
                .WithMany(u => u.Solicitudes)
                .HasForeignKey(s => s.JugadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
