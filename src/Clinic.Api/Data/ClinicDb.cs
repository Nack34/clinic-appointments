using Clinic.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Data;

public class ClinicDb(DbContextOptions<ClinicDb> options) : DbContext(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Doctor>(d =>
        {
            d.Property(x => x.FullName).HasMaxLength(120);
            d.Property(x => x.Specialty).HasMaxLength(80);
            d.HasMany(x => x.WorkingHours).WithOne().HasForeignKey(w => w.DoctorId);
        });

        model.Entity<Patient>(p =>
        {
            p.Property(x => x.FullName).HasMaxLength(120);
            p.Property(x => x.Dni).HasMaxLength(10);
            p.HasIndex(x => x.Dni).IsUnique();
        });

        model.Entity<Appointment>(a =>
        {
            a.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            a.Property(x => x.StartsAt).HasColumnType("timestamp without time zone");
            a.Property(x => x.EndsAt).HasColumnType("timestamp without time zone");
            // The database itself refuses two active appointments in the same slot, even if
            // two requests pass the availability check at the same moment.
            a.HasIndex(x => new { x.DoctorId, x.StartsAt })
                .IsUnique()
                .HasFilter("\"Status\" = 'Scheduled'");
            a.HasIndex(x => new { x.PatientId, x.StartsAt });
        });
    }
}
