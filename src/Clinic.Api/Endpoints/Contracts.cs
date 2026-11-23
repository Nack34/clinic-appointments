using System.ComponentModel.DataAnnotations;
using Clinic.Api.Domain;

namespace Clinic.Api.Endpoints;

public record DoctorView(int Id, string FullName, string Specialty, int SlotMinutes);

public record PatientView(int Id, string FullName, string Dni, string? Email, string? Phone);

public record CreatePatient(
    [property: Required, StringLength(120, MinimumLength = 3)] string FullName,
    [property: Required, RegularExpression(@"^\d{7,8}$", ErrorMessage = "DNI must have 7 or 8 digits")] string Dni,
    [property: EmailAddress] string? Email,
    [property: StringLength(30)] string? Phone);

public record BookAppointment(
    [property: Range(1, int.MaxValue)] int DoctorId,
    [property: Range(1, int.MaxValue)] int PatientId,
    DateTime StartsAt,
    [property: StringLength(300)] string? Reason);

public record CancelAppointment([property: StringLength(300)] string? Reason);

public record PersonRef(int Id, string FullName);

public record AppointmentView(
    int Id,
    PersonRef Doctor,
    string Specialty,
    PersonRef Patient,
    DateTime StartsAt,
    DateTime EndsAt,
    AppointmentStatus Status,
    string? Reason,
    string? CancellationReason)
{
    public static AppointmentView From(Appointment a) => new(
        a.Id, new PersonRef(a.Doctor.Id, a.Doctor.FullName), a.Doctor.Specialty,
        new PersonRef(a.Patient.Id, a.Patient.FullName), a.StartsAt, a.EndsAt, a.Status, a.Reason, a.CancellationReason);
}
