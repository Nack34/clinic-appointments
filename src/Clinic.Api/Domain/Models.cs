namespace Clinic.Api.Domain;

public class Doctor
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Specialty { get; set; }

    /// <summary>Length of one appointment in minutes. Slots start at the beginning of each shift.</summary>
    public int SlotMinutes { get; set; } = 30;

    public List<WorkingHours> WorkingHours { get; set; } = [];
}

/// <summary>A shift: e.g. Monday 09:00-13:00. A doctor can have several shifts per day.</summary>
public class WorkingHours
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public DayOfWeek Day { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

public class Patient
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Dni { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public enum AppointmentStatus
{
    Scheduled,
    Cancelled,
    Completed,
}

public class Appointment
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>Clinic local time (the clinic works in a single time zone).</summary>
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public string? Reason { get; set; }
    public string? CancellationReason { get; set; }
}
