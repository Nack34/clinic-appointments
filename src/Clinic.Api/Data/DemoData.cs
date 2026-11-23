using Clinic.Api.Domain;

namespace Clinic.Api.Data;

public static class DemoData
{
    public static void Seed(ClinicDb db)
    {
        if (db.Doctors.Any()) return;

        static List<WorkingHours> Weekdays(params (int start, int end)[] shifts) =>
            [.. Enum.GetValues<DayOfWeek>()
                .Where(d => d is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                .SelectMany(day => shifts.Select(s => new WorkingHours
                {
                    Day = day, Start = new TimeOnly(s.start, 0), End = new TimeOnly(s.end, 0),
                }))];

        db.Doctors.AddRange(
            new Doctor { FullName = "Dra. Laura Gómez", Specialty = "Clínica médica", WorkingHours = Weekdays((9, 13), (14, 17)) },
            new Doctor { FullName = "Dr. Martín Álvarez", Specialty = "Pediatría", SlotMinutes = 20, WorkingHours = Weekdays((8, 12)) },
            new Doctor
            {
                FullName = "Dra. Sofía Romero", Specialty = "Cardiología", SlotMinutes = 40,
                WorkingHours =
                [
                    new() { Day = DayOfWeek.Tuesday, Start = new(10, 0), End = new(14, 0) },
                    new() { Day = DayOfWeek.Thursday, Start = new(10, 0), End = new(14, 0) },
                ],
            });
        db.Patients.AddRange(
            new Patient { FullName = "Juan Pérez", Dni = "30111222", Email = "juan@example.com" },
            new Patient { FullName = "María Fernández", Dni = "28999888", Phone = "+54 221 555-0101" });
        db.SaveChanges();
    }
}
