using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using NoteDomain.Model;
using File = NoteDomain.Model.File;

namespace NoteInfrastructure;

public partial class NotedbContext : DbContext
{
    public NotedbContext() { }

    public NotedbContext(DbContextOptions<NotedbContext> options)
        : base(options) { }

    public virtual DbSet<File>        Files        { get; set; }
    public virtual DbSet<Fileversion> Fileversions { get; set; }
    public virtual DbSet<Folder>      Folders      { get; set; }
    public virtual DbSet<Tag>         Tags         { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<File>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("files_pkey");
            entity.ToTable("files");

            entity.HasIndex(e => new { e.Name, e.Folderid })
                  .IsUnique()
                  .HasDatabaseName("IX_Files_Name_Folderid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .HasColumnType("timestamp without time zone")
                  .HasColumnName("createdat");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Folderid).HasColumnName("folderid");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");

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

            entity.HasIndex(e => new { e.Name, e.Parentfolderid })
                  .IsUnique()
                  .HasDatabaseName("IX_Folders_Name_Parentfolderid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .HasColumnType("timestamp without time zone")
                  .HasColumnName("createdat");
            entity.Property(e => e.Name).HasMaxLength(100).HasColumnName("name");
            entity.Property(e => e.Parentfolderid).HasColumnName("parentfolderid");

            entity.Property(e => e.UserId)
                  .HasMaxLength(450)
                  .HasColumnName("userid");

            entity.HasOne(d => d.Parentfolder).WithMany(p => p.InverseParentfolder)
                  .HasForeignKey(d => d.Parentfolderid)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("folders_parentfolderid_fkey");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tags_pkey");
            entity.ToTable("tags");

            entity.HasIndex(e => new { e.Name, e.UserId })
                  .IsUnique()
                  .HasDatabaseName("tags_name_userid_key");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasMaxLength(15).HasColumnName("name");

            entity.Property(e => e.UserId)
                  .HasMaxLength(450)
                  .HasColumnName("userid");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
