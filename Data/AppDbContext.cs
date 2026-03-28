using JobHunterBot.Models;
using Microsoft.EntityFrameworkCore;

namespace JobHunterBot.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vaga> Vagas { get; set; }
    public DbSet<UsuarioConfig> UsuariosConfig { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Garante unicidade lógica para evitar vagas repetidas
        modelBuilder.Entity<Vaga>().HasIndex(v => v.Url).IsUnique();
        
        base.OnModelCreating(modelBuilder);
    }
}