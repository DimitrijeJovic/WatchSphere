using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Models;

namespace WatchSphere.API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Media> Media { get; set; }

        public DbSet<Genre> Genres { get; set; }

        public DbSet<MediaGenre> MediaGenres { get; set; }

        public DbSet<UserLibrary> UserLibraries { get; set; }

        public DbSet<Review> Reviews { get; set; }

        public DbSet<ReviewComment> ReviewComments { get; set; }

        public DbSet<ReviewVote> ReviewVotes { get; set; }

        public DbSet<Season> Seasons { get; set; }

        public DbSet<Episode> Episodes { get; set; }

        public DbSet<UserEpisode> UserEpisodes { get; set; }

        public DbSet<MediaRelation> MediaRelations { get; set; }

        public DbSet<Person> People { get; set; }

        public DbSet<MediaCredit> MediaCredits { get; set; }

        public DbSet<Studio> Studios { get; set; }

        public DbSet<MediaStudio> MediaStudios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Lengths matter here: without them Pomelo maps every string to
            // LONGTEXT, which MySQL refuses to index without a prefix length.
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(254).IsRequired();
                entity.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
                entity.Property(u => u.Role).HasMaxLength(20).IsRequired();
                entity.Property(u => u.AvatarUrl).HasMaxLength(500);
                entity.Property(u => u.Bio).HasMaxLength(500);

                entity.HasIndex(u => u.Email).IsUnique();
                entity.HasIndex(u => u.Username).IsUnique();
            });

            modelBuilder.Entity<Media>(entity =>
            {
                entity.Property(m => m.Title).HasMaxLength(200).IsRequired();
                entity.Property(m => m.Description).HasMaxLength(4000);
                entity.Property(m => m.Type).HasMaxLength(20).IsRequired();
                entity.Property(m => m.AiringStatus).HasMaxLength(20).IsRequired();
                entity.Property(m => m.PosterUrl).HasMaxLength(500);
                entity.Property(m => m.BackdropUrl).HasMaxLength(500);
                entity.Property(m => m.Studio).HasMaxLength(120);
                entity.Property(m => m.OriginalTitle).HasMaxLength(200);
                entity.Property(m => m.TrailerUrl).HasMaxLength(500);
                entity.Property(m => m.ImdbId).HasMaxLength(20);
                entity.Property(m => m.Source).HasMaxLength(20);

                entity.HasIndex(m => m.Title);
                entity.HasIndex(m => m.Type);
                entity.HasIndex(m => m.CreatedAt);

                // Unique so the same title cannot be imported twice. MySQL
                // permits repeated NULLs here, so hand-added titles that have
                // no external counterpart are unaffected.
                entity.HasIndex(m => m.TmdbId).IsUnique();
                entity.HasIndex(m => m.KitsuId).IsUnique();
                entity.HasIndex(m => m.MalId).IsUnique();
                entity.HasIndex(m => m.AniListId).IsUnique();
                entity.HasIndex(m => m.TvMazeId).IsUnique();

                // The id every catalogue agrees on, and so the one that stops
                // the same title arriving twice from two different providers.
                entity.HasIndex(m => m.ImdbId).IsUnique();
            });

            modelBuilder.Entity<Genre>(entity =>
            {
                entity.Property(g => g.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(g => g.Name).IsUnique();
            });

            modelBuilder.Entity<MediaGenre>(entity =>
            {
                entity.HasKey(mg => new { mg.MediaId, mg.GenreId });

                entity.HasOne(mg => mg.Media)
                      .WithMany(m => m.MediaGenres)
                      .HasForeignKey(mg => mg.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(mg => mg.Genre)
                      .WithMany(g => g.MediaGenres)
                      .HasForeignKey(mg => mg.GenreId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserLibrary>(entity =>
            {
                entity.Property(ul => ul.Status).HasMaxLength(20).IsRequired();
                entity.Property(ul => ul.Notes).HasMaxLength(1000);

                // The controller also checks, but only the index makes a
                // duplicate impossible under concurrent requests.
                entity.HasIndex(ul => new { ul.UserId, ul.MediaId }).IsUnique();
                entity.HasIndex(ul => new { ul.UserId, ul.Status });

                entity.HasOne(ul => ul.User)
                      .WithMany(u => u.UserLibraries)
                      .HasForeignKey(ul => ul.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ul => ul.Media)
                      .WithMany(m => m.UserLibraries)
                      .HasForeignKey(ul => ul.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Review>(entity =>
            {
                entity.Property(r => r.ReviewText).HasMaxLength(4000).IsRequired();

                entity.HasIndex(r => new { r.UserId, r.MediaId }).IsUnique();
                entity.HasIndex(r => r.MediaId);

                entity.HasOne(r => r.User)
                      .WithMany(u => u.Reviews)
                      .HasForeignKey(r => r.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Media)
                      .WithMany(m => m.Reviews)
                      .HasForeignKey(r => r.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReviewComment>(entity =>
            {
                entity.Property(c => c.Text).HasMaxLength(1000).IsRequired();

                entity.HasIndex(c => c.ReviewId);

                entity.HasOne(c => c.Review)
                      .WithMany(r => r.Comments)
                      .HasForeignKey(c => c.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.User)
                      .WithMany(u => u.ReviewComments)
                      .HasForeignKey(c => c.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Person>(entity =>
            {
                entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
                entity.Property(p => p.PhotoUrl).HasMaxLength(500);

                entity.HasIndex(p => p.Name);

                // Nullable and unique, so a person imported from two sources
                // ends up as one row carrying both ids.
                entity.HasIndex(p => p.TmdbId).IsUnique();
                entity.HasIndex(p => p.TvMazeId).IsUnique();
                entity.HasIndex(p => p.KitsuId).IsUnique();
            });

            modelBuilder.Entity<MediaCredit>(entity =>
            {
                entity.Property(c => c.Role).HasMaxLength(20).IsRequired();
                entity.Property(c => c.CharacterName).HasMaxLength(200);
                entity.Property(c => c.CharacterImageUrl).HasMaxLength(500);
                entity.Property(c => c.Language).HasMaxLength(30);

                // One person can hold several roles on a title, and voice the
                // same character in more than one language, but not twice over.
                entity.HasIndex(c => new { c.MediaId, c.PersonId, c.Role, c.CharacterName, c.Language })
                      .IsUnique();

                entity.HasIndex(c => c.PersonId);

                entity.HasOne(c => c.Media)
                      .WithMany(m => m.Credits)
                      .HasForeignKey(c => c.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Person)
                      .WithMany(p => p.Credits)
                      .HasForeignKey(c => c.PersonId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Studio>(entity =>
            {
                entity.Property(st => st.Name).HasMaxLength(150).IsRequired();
                entity.Property(st => st.LogoUrl).HasMaxLength(500);

                entity.HasIndex(st => st.Name).IsUnique();
                entity.HasIndex(st => st.TmdbId).IsUnique();
            });

            modelBuilder.Entity<MediaStudio>(entity =>
            {
                entity.HasKey(ms => new { ms.MediaId, ms.StudioId });

                entity.HasOne(ms => ms.Media)
                      .WithMany(m => m.MediaStudios)
                      .HasForeignKey(ms => ms.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ms => ms.Studio)
                      .WithMany(st => st.MediaStudios)
                      .HasForeignKey(ms => ms.StudioId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<MediaRelation>(entity =>
            {
                entity.HasKey(r => new { r.MediaId, r.RelatedMediaId });

                entity.Property(r => r.RelationType).HasMaxLength(30).IsRequired();

                entity.HasIndex(r => r.RelatedMediaId);

                entity.HasOne(r => r.Media)
                      .WithMany(m => m.Relations)
                      .HasForeignKey(r => r.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Both ends cascade so removing a title takes its links with
                // it whichever side of the pair it sits on.
                entity.HasOne(r => r.RelatedMedia)
                      .WithMany(m => m.RelatedFrom)
                      .HasForeignKey(r => r.RelatedMediaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Season>(entity =>
            {
                entity.Property(s => s.Name).HasMaxLength(200);
                entity.Property(s => s.Overview).HasMaxLength(2000);
                entity.Property(s => s.PosterUrl).HasMaxLength(500);

                entity.HasIndex(s => new { s.MediaId, s.SeasonNumber }).IsUnique();

                entity.HasOne(s => s.Media)
                      .WithMany(m => m.Seasons)
                      .HasForeignKey(s => s.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Episode>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(300);
                entity.Property(e => e.Overview).HasMaxLength(2000);
                entity.Property(e => e.StillUrl).HasMaxLength(500);

                entity.HasIndex(e => new { e.SeasonId, e.EpisodeNumber }).IsUnique();
                entity.HasIndex(e => new { e.MediaId, e.AbsoluteNumber });

                entity.HasOne(e => e.Season)
                      .WithMany(s => s.Episodes)
                      .HasForeignKey(e => e.SeasonId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Cascades as well, even though deleting a title already
                // reaches these rows through Season. InnoDB allows a row to be
                // removed by two paths; restricting instead would block the
                // delete outright, since the constraint is checked whatever
                // the other path would have done.
                entity.HasOne(e => e.Media)
                      .WithMany(m => m.Episodes)
                      .HasForeignKey(e => e.MediaId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserEpisode>(entity =>
            {
                entity.HasKey(ue => new { ue.UserId, ue.EpisodeId });

                entity.HasIndex(ue => ue.EpisodeId);

                entity.HasOne(ue => ue.User)
                      .WithMany(u => u.WatchedEpisodes)
                      .HasForeignKey(ue => ue.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ue => ue.Episode)
                      .WithMany(e => e.UserEpisodes)
                      .HasForeignKey(ue => ue.EpisodeId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ReviewVote>(entity =>
            {
                entity.HasKey(v => new { v.ReviewId, v.UserId });

                entity.HasOne(v => v.Review)
                      .WithMany(r => r.Votes)
                      .HasForeignKey(v => v.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(v => v.User)
                      .WithMany(u => u.ReviewVotes)
                      .HasForeignKey(v => v.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
