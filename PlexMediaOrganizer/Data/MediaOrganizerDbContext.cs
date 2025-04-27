using Microsoft.EntityFrameworkCore;
using PlexMediaOrganizer.Data.Entities;

namespace PlexMediaOrganizer.Data;

public class MediaOrganizerDbContext : DbContext
{
    public MediaOrganizerDbContext(DbContextOptions<MediaOrganizerDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessedFile> ProcessedFiles { get; set; } = null!;
    public DbSet<MediaItem> MediaItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ProcessedFile entity
        modelBuilder.Entity<ProcessedFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourcePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.DestinationPath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.FileSize).IsRequired();
            entity.Property(e => e.FileHash).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ProcessedDate).IsRequired();
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            // Create index on FileHash for faster duplicate detection
            entity.HasIndex(e => e.FileHash);
            
            // Create index on SourcePath for faster lookups
            entity.HasIndex(e => e.SourcePath);
        });

        // Configure MediaItem entity
        modelBuilder.Entity<MediaItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MediaType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ImdbId).HasMaxLength(20);
            entity.Property(e => e.EpisodeTitle).HasMaxLength(255);
            entity.Property(e => e.DateAdded).IsRequired();
            entity.Property(e => e.LastUpdated).IsRequired();

            // Create indexes for faster lookups
            entity.HasIndex(e => new { e.Title, e.Year });
            entity.HasIndex(e => e.TmdbId);
            entity.HasIndex(e => e.ImdbId);
            entity.HasIndex(e => e.TvdbId);
            
            // Configure relationship with ProcessedFiles
            entity.HasMany(e => e.Files)
                  .WithOne(e => e.MediaItem)
                  .HasForeignKey(e => e.MediaItemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
