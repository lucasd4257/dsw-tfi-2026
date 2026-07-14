using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Data;

public class Dsw2026TpiDbContext : DbContext
{
    public Dsw2026TpiDbContext(DbContextOptions<Dsw2026TpiDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Speciality> Specialities => Set<Speciality>();
    public DbSet<AvailabilityRule> AvailabilityRules => Set<AvailabilityRule>();
    public DbSet<AvailabilitySlot> AvailabilitySlots => Set<AvailabilitySlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Patient ----------
        // Nota: ApplicationUserId es una FK "lógica" hacia ApplicationUser (Identity),
        // que vive en AuthenticationDbContext. No se puede modelar como FK real de EF
        // porque son contextos distintos, aunque compartan la misma base física.
        modelBuilder.Entity<Patient>(e =>
        {
            e.ToTable("Patients");
            e.Property(p => p.Dni).HasMaxLength(10).IsRequired();
            e.HasIndex(p => p.Dni).IsUnique();
            e.Property(p => p.FullName).HasMaxLength(150).IsRequired();
            e.Property(p => p.ApplicationUserId).HasMaxLength(450).IsRequired();
            e.HasIndex(p => p.ApplicationUserId).IsUnique();

            e.HasQueryFilter(p => p.IsActive);
        });

        // ---------- Speciality ----------
        modelBuilder.Entity<Speciality>(e =>
        {
            e.ToTable("Specialities");
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Description).HasMaxLength(100).IsRequired();
        });

        // ---------- Doctor ----------
        modelBuilder.Entity<Doctor>(e =>
        {
            e.ToTable("Doctors");
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.LicenseNumber).HasMaxLength(100).IsRequired();
            e.HasIndex(p => p.LicenseNumber).IsUnique();

            e.HasOne(p => p.Speciality)
             .WithMany()
             .HasForeignKey(p => p.SpecialityId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasQueryFilter(p => p.IsActive);
        });

        // ---------- AvailabilityRule ----------
        modelBuilder.Entity<AvailabilityRule>(e =>
        {
            e.ToTable("AvailabilityRules");
            e.Property(p => p.DayOfWeek).HasMaxLength(20).IsRequired();

            e.HasOne(p => p.Doctor)
             .WithMany()
             .HasForeignKey(p => p.DoctorId)
             .OnDelete(DeleteBehavior.Restrict);

            // RN: no se permiten reglas duplicadas para el mismo médico/mes/año/día/horario
            e.HasIndex(p => new { p.DoctorId, p.Year, p.Month, p.DayOfWeek, p.StartTime, p.EndTime })
             .IsUnique();

            e.HasQueryFilter(p => p.IsActive);
        });

        // ---------- AvailabilitySlot ----------
        modelBuilder.Entity<AvailabilitySlot>(e =>
        {
            e.ToTable("AvailabilitySlots");
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            e.HasOne(p => p.AvailabilityRule)
             .WithMany()
             .HasForeignKey(p => p.AvailabilityRuleId)
             .OnDelete(DeleteBehavior.Restrict);

            // RN03: sin superposición para el mismo médico/fecha/hora de inicio
            e.HasIndex(p => new { p.AvailabilityRuleId, p.SlotDate, p.StartTime })
             .IsUnique();

            e.HasQueryFilter(p => p.IsActive);
        });

        // ---------- Appointment ----------
        modelBuilder.Entity<Appointment>(e =>
        {
            e.ToTable("Appointments");
            e.Property(p => p.Reason).HasMaxLength(300).IsRequired();
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

            e.HasOne(p => p.AvailabilitySlot)
             .WithMany()
             .HasForeignKey(p => p.AvailabilitySlotId)
             .OnDelete(DeleteBehavior.Restrict);

            // Un slot solo puede tener un turno asociado (evita doble reserva a nivel BD)
            e.HasIndex(p => p.AvailabilitySlotId).IsUnique();

            e.HasOne(p => p.Patient)
             .WithMany()
             .HasForeignKey(p => p.PatientId)
             .OnDelete(DeleteBehavior.Restrict);

            // Control de concurrencia optimista (RN03 / control de concurrencia en reservas)
            e.Property<byte[]>("RowVersion").IsRowVersion();
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<EntityBase>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}