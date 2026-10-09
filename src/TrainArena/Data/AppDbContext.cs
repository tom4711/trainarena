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
            e.Property(x => x.Option4).HasMaxLength(400).IsRequired();
            e.Property(x => x.Option5).HasMaxLength(400).IsRequired();
            e.Property(x => x.DisplayKind).HasConversion<int>();
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
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (!columns.Contains("ImagePath"))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Questions ADD COLUMN ImagePath TEXT NULL;";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!columns.Contains("Option4"))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Questions ADD COLUMN Option4 TEXT NOT NULL DEFAULT '';";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!columns.Contains("Option5"))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Questions ADD COLUMN Option5 TEXT NOT NULL DEFAULT '';";
            await alter.ExecuteNonQueryAsync(ct);
        }

        if (!columns.Contains("DisplayKind"))
        {
            await using var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Questions ADD COLUMN DisplayKind INTEGER NOT NULL DEFAULT 0;";
            await alter.ExecuteNonQueryAsync(ct);
        }
    }
}
