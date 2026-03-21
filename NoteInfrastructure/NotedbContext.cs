using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure;

public partial class NotedbContext : DbContext
{
    public NotedbContext()
    {
    }

    public NotedbContext(DbContextOptions<NotedbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<File> Files { get; set; }

    public virtual DbSet<Fileversion> Fileversions { get; set; }

    public virtual DbSet<Folder> Folders { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=notedb;Username=nermline;Password=KolKa84652kn16");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<File>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("files_pkey");

            entity.ToTable("files");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Folderid).HasColumnName("folderid");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");

            entity.HasOne(d => d.Folder).WithMany(p => p.Files)
                .HasForeignKey(d => d.Folderid)
                .HasConstraintName("files_folderid_fkey");

            entity.HasMany(d => d.Tags).WithMany(p => p.Files)
                .UsingEntity<Dictionary<string, object>>(
                    "Filetag",
                    r => r.HasOne<Tag>().WithMany()
                        .HasForeignKey("Tagid")
                        .HasConstraintName("filetags_tagid_fkey"),
                    l => l.HasOne<File>().WithMany()
                        .HasForeignKey("Fileid")
                        .HasConstraintName("filetags_fileid_fkey"),
                    j =>
                    {
                        j.HasKey("Fileid", "Tagid").HasName("filetags_pkey");
                        j.ToTable("filetags");
                        j.IndexerProperty<int>("Fileid").HasColumnName("fileid");
                        j.IndexerProperty<int>("Tagid").HasColumnName("tagid");
                    });
        });

        modelBuilder.Entity<Fileversion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("fileversions_pkey");

            entity.ToTable("fileversions");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Changelog).HasColumnName("changelog");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Fileid).HasColumnName("fileid");
            entity.Property(e => e.Versionnumber).HasColumnName("versionnumber");

            entity.HasOne(d => d.File).WithMany(p => p.Fileversions)
                .HasForeignKey(d => d.Fileid)
                .HasConstraintName("fileversions_fileid_fkey");
        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("folders_pkey");

            entity.ToTable("folders");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("createdat");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Parentfolderid).HasColumnName("parentfolderid");

            entity.HasOne(d => d.Parentfolder).WithMany(p => p.InverseParentfolder)
                .HasForeignKey(d => d.Parentfolderid)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("folders_parentfolderid_fkey");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");

            entity.ToTable("tags");

            entity.HasIndex(e => e.Name, "tags_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(15)
                .HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
