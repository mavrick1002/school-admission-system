using Microsoft.EntityFrameworkCore;

namespace Admission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("classes", table =>
                table.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));
            entity.HasKey(c => c.Id).HasName("pk_classes");
            entity.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(c => c.Name).HasColumnName("name");
            entity.Property(c => c.TotalSeats).HasColumnName("total_seats");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Hold>(entity =>
        {
            entity.ToTable("holds", table =>
                table.HasCheckConstraint(
                    "ck_holds_status",
                    "status IN ('HELD', 'CONFIRMED', 'EXPIRED', 'CANCELLED')"));
            entity.HasKey(h => h.Id).HasName("pk_holds");
            entity.Property(h => h.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(h => h.ClassId).HasColumnName("class_id");
            entity.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(17);
            entity.Property(h => h.Status).HasColumnName("status");
            entity.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            entity.Property(h => h.CreatedAt).HasColumnName("created_at");

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .HasConstraintName("fk_holds_classes_class_id")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => new { h.ClassId, h.StudentId })
                .HasDatabaseName("ux_holds_active_class_student")
                .IsUnique()
                .HasFilter("status IN ('HELD', 'CONFIRMED')");
        });
    }
}
