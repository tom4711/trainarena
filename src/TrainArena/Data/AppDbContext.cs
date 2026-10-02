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
            e.HasIndex(x => new { x.QuizId, x.SortOrder });
        });
    }
}
