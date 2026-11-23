using Clinic.Api.Data;
using Clinic.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Scheduling;

/// <summary>A broken business rule, returned to the client as a ProblemDetails response.</summary>
public class SchedulingException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public record Slot(DateTime StartsAt, DateTime EndsAt);

/// <summary>All appointment rules live here: availability, booking and cancellation.</summary>
public class SchedulingService(ClinicDb db, ClinicClock clock)
{
    public static readonly TimeSpan MinCancellationNotice = TimeSpan.FromHours(2);
    public const int MaxDaysAhead = 90;

    /// <summary>Free slots of a doctor on a date, in clinic local time. Past slots are left out.</summary>
    public async Task<List<Slot>> AvailabilityAsync(int doctorId, DateOnly date, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.Include(d => d.WorkingHours).FirstOrDefaultAsync(d => d.Id == doctorId, ct)
            ?? throw new SchedulingException(404, "Doctor not found");

        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var taken = await db.Appointments
            .Where(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Scheduled
                        && a.StartsAt >= dayStart && a.StartsAt < dayStart.AddDays(1))
            .Select(a => a.StartsAt)
            .ToListAsync(ct);
        var takenSet = taken.ToHashSet();
        var now = clock.Now;

        return [.. AllSlots(doctor, date).Where(s => s.StartsAt > now && !takenSet.Contains(s.StartsAt))];
    }

    public async Task<Appointment> BookAsync(int doctorId, int patientId, DateTime startsAt, string? reason, CancellationToken ct = default)
    {
        var doctor = await db.Doctors.Include(d => d.WorkingHours).FirstOrDefaultAsync(d => d.Id == doctorId, ct)
            ?? throw new SchedulingException(404, "Doctor not found");
        if (!await db.Patients.AnyAsync(p => p.Id == patientId, ct))
            throw new SchedulingException(404, "Patient not found");

        startsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Unspecified);
        if (startsAt <= clock.Now)
            throw new SchedulingException(400, "The appointment must be in the future");
        if (startsAt > clock.Now.AddDays(MaxDaysAhead))
            throw new SchedulingException(400, $"Appointments can be booked up to {MaxDaysAhead} days ahead");

        var slot = AllSlots(doctor, DateOnly.FromDateTime(startsAt)).FirstOrDefault(s => s.StartsAt == startsAt)
            ?? throw new SchedulingException(400, "The doctor doesn't attend at that time");

        var slotTaken = await db.Appointments.AnyAsync(a =>
            a.DoctorId == doctorId && a.Status == AppointmentStatus.Scheduled && a.StartsAt == slot.StartsAt, ct);
        if (slotTaken)
            throw new SchedulingException(409, "That slot is already booked");

        var patientBusy = await db.Appointments.AnyAsync(a =>
            a.PatientId == patientId && a.Status == AppointmentStatus.Scheduled
            && a.StartsAt < slot.EndsAt && slot.StartsAt < a.EndsAt, ct);
        if (patientBusy)
            throw new SchedulingException(409, "The patient already has an appointment at that time");

        var appointment = new Appointment
        {
            DoctorId = doctorId, PatientId = patientId, StartsAt = slot.StartsAt, EndsAt = slot.EndsAt,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
        db.Appointments.Add(appointment);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index caught a double booking (another request took the slot first).
            throw new SchedulingException(409, "That slot was just taken");
        }
        return appointment;
    }

    public async Task<Appointment> CancelAsync(int appointmentId, string? reason, CancellationToken ct = default)
    {
        var appointment = await db.Appointments.FindAsync([appointmentId], ct)
            ?? throw new SchedulingException(404, "Appointment not found");
        if (appointment.Status != AppointmentStatus.Scheduled)
            throw new SchedulingException(409, $"The appointment is already {appointment.Status.ToString().ToLowerInvariant()}");
        if (appointment.StartsAt - clock.Now < MinCancellationNotice)
            throw new SchedulingException(409, "Appointments can only be cancelled at least 2 hours in advance");

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await db.SaveChangesAsync(ct);
        return appointment;
    }

    /// <summary>Every slot of the doctor's shifts on that date, ignoring bookings.</summary>
    public static IEnumerable<Slot> AllSlots(Doctor doctor, DateOnly date)
    {
        var length = TimeSpan.FromMinutes(doctor.SlotMinutes);
        foreach (var shift in doctor.WorkingHours.Where(w => w.Day == date.DayOfWeek).OrderBy(w => w.Start))
        {
            var start = date.ToDateTime(shift.Start);
            var end = date.ToDateTime(shift.End);
            for (var t = start; t + length <= end; t += length)
                yield return new Slot(t, t + length);
        }
    }
}

/// <summary>"Now" in the clinic's time zone. Built on TimeProvider so tests can freeze time.</summary>
public class ClinicClock(TimeProvider time, TimeZoneInfo zone)
{
    public DateTime Now => DateTime.SpecifyKind(
        TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime, DateTimeKind.Unspecified);
}
