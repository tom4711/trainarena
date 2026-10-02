using Microsoft.EntityFrameworkCore;
using TrainArena.Data.Entities;

namespace TrainArena.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<Question> Questions => Set<Question>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quiz>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Questions)
                .WithOne(x => x.Quiz!)
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Option0).HasMaxLength(400).IsRequired();
            e.Property(x => x.Option1).HasMaxLength(400).IsRequired();
            e.Property(x => x.Option2).HasMaxLength(400).IsRequired();
            e.Property(x => x.Option3).HasMaxLength(400).IsRequired();
            e.Property(x => x.ImagePath).HasMaxLength(400);
            e.HasIndex(x => new { x.QuizId, x.SortOrder });
        });
    }

    /// <summary>
    /// EnsureCreated does not add columns to existing DBs — patch ImagePath if missing.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await Database.EnsureCreatedAsync(ct);
        await using var conn = Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Questions');";
        var hasImagePath = false;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(1);
                if (string.Equals(name, "ImagePath", StringComparison.OrdinalIgnoreCase))
                {
                    hasImagePath = true;
                    break;
                }
            }
        }

        if (!hasImagePath)
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Questions ADD COLUMN ImagePath TEXT NULL;";
            await alter.ExecuteNonQueryAsync(ct);
        }
    }
}
