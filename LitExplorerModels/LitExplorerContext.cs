using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace LitExplorerAPI.LitExplorerModels;

public partial class LitExplorerContext : DbContext
{
    public LitExplorerContext()
    {
    }

    public LitExplorerContext(DbContextOptions<LitExplorerContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Author> Authors { get; set; }

    public virtual DbSet<Book> Books { get; set; }

    public virtual DbSet<BooksMetum> BooksMeta { get; set; }

    public virtual DbSet<BooksSource> BooksSources { get; set; }

    public virtual DbSet<Source> Sources { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<TagsCategory> TagsCategories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.AuthorId).HasName("PK__Authors__70DAFC34BE9EC877");

            entity.HasIndex(e => e.AuthorName, "UQ__Authors__4A1A120B93DEA43B").IsUnique();

            entity.Property(e => e.AuthorName).HasMaxLength(255);
        });

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(e => e.BookId).HasName("PK__Books__3DE0C207594F6D52");

            entity.HasIndex(e => e.Title, "UQ__Books__2CB664DC5927EFDC").IsUnique();

            entity.Property(e => e.Title).HasMaxLength(500);
        });

        modelBuilder.Entity<BooksMetum>(entity =>
        {
            entity.HasKey(e => e.BookSourceId).HasName("PK__BooksMet__F2DA0FE492376E1F");

            entity.Property(e => e.BookSourceId).ValueGeneratedNever();
            entity.Property(e => e.CoverImageUrl)
                .HasMaxLength(2048)
                .HasColumnName("CoverImageURL");
            entity.Property(e => e.FirstChapterReleaseDate).HasColumnType("datetime");
            entity.Property(e => e.LastChapterReleaseDate).HasColumnType("datetime");

            entity.HasOne(d => d.Author).WithMany(p => p.BooksMeta)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("FK__BooksMeta__Autho__45F365D3");

            entity.HasOne(d => d.BookSource).WithOne(p => p.BooksMetum)
                .HasForeignKey<BooksMetum>(d => d.BookSourceId)
                .HasConstraintName("FK__BooksMeta__BookS__44FF419A");
        });

        modelBuilder.Entity<BooksSource>(entity =>
        {
            entity.HasKey(e => e.BookSourceId).HasName("PK__BooksSou__F2DA0FE4AEFEE818");

            entity.HasIndex(e => new { e.BookId, e.SourceId }, "UQ_BookSource").IsUnique();

            entity.Property(e => e.SiteUrl)
                .HasMaxLength(2048)
                .HasColumnName("SiteURL");

            entity.HasOne(d => d.Book).WithMany(p => p.BooksSources)
                .HasForeignKey(d => d.BookId)
                .HasConstraintName("FK__BooksSour__BookI__412EB0B6");

            entity.HasOne(d => d.Source).WithMany(p => p.BooksSources)
                .HasForeignKey(d => d.SourceId)
                .HasConstraintName("FK__BooksSour__Sourc__4222D4EF");

            entity.HasMany(d => d.Tags).WithMany(p => p.BookSources)
                .UsingEntity<Dictionary<string, object>>(
                    "BooksTag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("TagId")
                        .HasConstraintName("FK__BooksTags__TagId__5070F446"),
                    l => l.HasOne<BooksSource>().WithMany()
                        .HasForeignKey("BookSourceId")
                        .HasConstraintName("FK__BooksTags__BookS__4F7CD00D"),
                    j =>
                    {
                        j.HasKey("BookSourceId", "TagId").HasName("PK__BooksTag__248DC07E2F2D0E9C");
                        j.ToTable("BooksTags");
                    });
        });

        modelBuilder.Entity<Source>(entity =>
        {
            entity.HasKey(e => e.SourceId).HasName("PK__Sources__16E019196224EF5A");

            entity.HasIndex(e => e.SourceName, "UQ__Sources__3C28DC171C5082B1").IsUnique();

            entity.Property(e => e.HomePageUrl)
                .HasMaxLength(2048)
                .HasColumnName("HomePageURL");
            entity.Property(e => e.SourceName).HasMaxLength(255);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.TagId).HasName("PK__Tags__657CF9AC4BF23645");

            entity.HasIndex(e => e.TagName, "UQ__Tags__BDE0FD1D1729EF8A").IsUnique();

            entity.Property(e => e.TagName).HasMaxLength(255);

            entity.HasOne(d => d.Category).WithMany(p => p.Tags)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Tags__CategoryId__4CA06362");
        });

        modelBuilder.Entity<TagsCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__TagsCate__19093A0BE92E55C9");

            entity.HasIndex(e => e.CategoryName, "UQ__TagsCate__8517B2E0842835FB").IsUnique();

            entity.Property(e => e.CategoryName).HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C05410897");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D1053479575192").IsUnique();

            entity.Property(e => e.Email)
                .HasMaxLength(320)
                .IsUnicode(false);
            entity.Property(e => e.HashedPassword)
                .HasMaxLength(60)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.RegistrationDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
