using Clinic.Api.Data;
using Clinic.Api.Domain;
using Clinic.Api.Scheduling;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Api.Endpoints;

public static class ClinicEndpoints
{
    public static void MapClinicEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // --- Doctors -----------------------------------------------------------------
        api.MapGet("/doctors", async (ClinicDb db, string? specialty) =>
        {
            var query = db.Doctors.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(specialty))
                query = query.Where(d => d.Specialty.ToLower().Contains(specialty.ToLower()));
            return await query.OrderBy(d => d.Specialty).ThenBy(d => d.FullName)
                .Select(d => new DoctorView(d.Id, d.FullName, d.Specialty, d.SlotMinutes))
                .ToListAsync();
        }).WithTags("Doctors");

        api.MapGet("/doctors/{id:int}/availability", async (int id, DateOnly date, SchedulingService scheduling) =>
            await scheduling.AvailabilityAsync(id, date))
            .WithTags("Doctors")
            .WithSummary("Free slots of a doctor on a date (clinic local time)");

        // --- Patients ----------------------------------------------------------------
        api.MapGet("/patients", async (ClinicDb db, string? dni, string? name) =>
        {
            var query = db.Patients.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(dni)) query = query.Where(p => p.Dni == dni.Trim());
            if (!string.IsNullOrWhiteSpace(name)) query = query.Where(p => p.FullName.ToLower().Contains(name.ToLower()));
            return await query.OrderBy(p => p.FullName).Take(50)
                .Select(p => new PatientView(p.Id, p.FullName, p.Dni, p.Email, p.Phone))
                .ToListAsync();
        }).WithTags("Patients");

        api.MapPost("/patients", async Task<Results<Created<PatientView>, Conflict<string>>> (CreatePatient body, ClinicDb db) =>
        {
            if (await db.Patients.AnyAsync(p => p.Dni == body.Dni))
                return TypedResults.Conflict("A patient with that DNI already exists");
            var patient = new Patient
            {
                FullName = body.FullName.Trim(), Dni = body.Dni, Email = body.Email?.Trim(), Phone = body.Phone?.Trim(),
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/patients/{patient.Id}",
                new PatientView(patient.Id, patient.FullName, patient.Dni, patient.Email, patient.Phone));
        }).WithTags("Patients");

        // --- Appointments ------------------------------------------------------------
        api.MapGet("/appointments", async (ClinicDb db, int? doctorId, int? patientId, DateOnly? date, AppointmentStatus? status) =>
        {
            var query = db.Appointments.AsNoTracking().Include(a => a.Doctor).Include(a => a.Patient).AsQueryable();
            if (doctorId is not null) query = query.Where(a => a.DoctorId == doctorId);
            if (patientId is not null) query = query.Where(a => a.PatientId == patientId);
            if (status is not null) query = query.Where(a => a.Status == status);
            if (date is not null)
            {
                var start = date.Value.ToDateTime(TimeOnly.MinValue);
                query = query.Where(a => a.StartsAt >= start && a.StartsAt < start.AddDays(1));
            }
            var items = await query.OrderBy(a => a.StartsAt).Take(200).ToListAsync();
            return items.Select(AppointmentView.From);
        }).WithTags("Appointments");

        api.MapPost("/appointments", async (BookAppointment body, SchedulingService scheduling, ClinicDb db) =>
        {
            var created = await scheduling.BookAsync(body.DoctorId, body.PatientId, body.StartsAt, body.Reason);
            await db.Entry(created).Reference(a => a.Doctor).LoadAsync();
            await db.Entry(created).Reference(a => a.Patient).LoadAsync();
            return TypedResults.Created($"/api/appointments/{created.Id}", AppointmentView.From(created));
        }).WithTags("Appointments");

        api.MapPost("/appointments/{id:int}/cancel", async (int id, CancelAppointment? body, SchedulingService scheduling, ClinicDb db) =>
        {
            var cancelled = await scheduling.CancelAsync(id, body?.Reason);
            await db.Entry(cancelled).Reference(a => a.Doctor).LoadAsync();
            await db.Entry(cancelled).Reference(a => a.Patient).LoadAsync();
            return AppointmentView.From(cancelled);
        }).WithTags("Appointments");
    }
}
