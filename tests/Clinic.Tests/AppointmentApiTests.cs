using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clinic.Api.Endpoints;
using Clinic.Api.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Tests;

public class AppointmentApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ClinicFactory _factory = new();
    private readonly HttpClient _client;

    // Seed data: doctor 1 = clinical medicine, Mon-Fri 9-13 and 14-17, 30-minute slots.
    private const int Gomez = 1;
    private const int Juan = 1;
    private const int Maria = 2;

    public AppointmentApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> Book(int doctor, int patient, string startsAt, string? reason = null) =>
        _client.PostAsJsonAsync("/api/appointments", new { doctorId = doctor, patientId = patient, startsAt, reason });

    private async Task<string> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!;

    [Fact]
    public async Task Availability_lists_the_shifts_in_slots()
    {
        var slots = await _client.GetFromJsonAsync<List<Slot>>("/api/doctors/1/availability?date=2026-11-24", Json);
        Assert.Equal(14, slots!.Count); // 8 in the morning + 6 in the afternoon
        Assert.Equal(new DateTime(2026, 11, 24, 9, 0, 0), slots[0].StartsAt);
        Assert.Equal(new DateTime(2026, 11, 24, 16, 30, 0), slots[^1].StartsAt);
        Assert.DoesNotContain(slots, s => s.StartsAt.Hour == 13); // lunch break
    }

    [Fact]
    public async Task Availability_is_empty_on_weekends_and_hides_past_slots()
    {
        var saturday = await _client.GetFromJsonAsync<List<Slot>>("/api/doctors/1/availability?date=2026-11-28", Json);
        Assert.Empty(saturday!);

        _factory.Time.Advance(TimeSpan.FromHours(3)); // now it's 11:00 on Monday
        var today = await _client.GetFromJsonAsync<List<Slot>>("/api/doctors/1/availability?date=2026-11-23", Json);
        Assert.Equal(new DateTime(2026, 11, 23, 11, 30, 0), today![0].StartsAt);
    }

    [Fact]
    public async Task Booking_takes_the_slot_out_of_availability()
    {
        var response = await Book(Gomez, Juan, "2026-11-24T10:00:00", "Control anual");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var appointment = await response.Content.ReadFromJsonAsync<AppointmentView>(Json);
        Assert.Equal(new DateTime(2026, 11, 24, 10, 30, 0), appointment!.EndsAt);
        Assert.Equal("Juan Pérez", appointment.Patient.FullName);

        var slots = await _client.GetFromJsonAsync<List<Slot>>("/api/doctors/1/availability?date=2026-11-24", Json);
        Assert.DoesNotContain(slots!, s => s.StartsAt.Hour == 10 && s.StartsAt.Minute == 0);
    }

    [Theory]
    [InlineData("2026-11-24T10:00:00", HttpStatusCode.Conflict, "already booked")]
    public async Task The_same_slot_cannot_be_booked_twice(string startsAt, HttpStatusCode expected, string message)
    {
        await Book(Gomez, Juan, startsAt);
        var second = await Book(Gomez, Maria, startsAt);
        Assert.Equal(expected, second.StatusCode);
        Assert.Contains(message, await Problem(second));
    }

    [Theory]
    [InlineData("2026-11-24T08:30:00", "doesn't attend")] // before the shift
    [InlineData("2026-11-24T13:00:00", "doesn't attend")] // lunch break
    [InlineData("2026-11-24T10:15:00", "doesn't attend")] // not aligned to a slot
    [InlineData("2026-11-28T10:00:00", "doesn't attend")] // Saturday
    [InlineData("2026-11-20T10:00:00", "future")]         // past
    [InlineData("2027-06-01T10:00:00", "90 days")]        // too far ahead
    public async Task Invalid_times_are_rejected(string startsAt, string message)
    {
        var response = await Book(Gomez, Juan, startsAt);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, await Problem(response));
    }

    [Fact]
    public async Task A_patient_cannot_be_in_two_places_at_once()
    {
        await Book(Gomez, Juan, "2026-11-24T10:00:00");
        // Dr. Álvarez (id 2) has 20-minute slots: 10:20 overlaps nothing, 10:00 overlaps Juan's 10:00-10:30.
        var overlapping = await Book(2, Juan, "2026-11-24T10:00:00");
        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Contains("already has an appointment", await Problem(overlapping));
    }

    [Fact]
    public async Task Cancelling_frees_the_slot_and_respects_the_notice_period()
    {
        var booked = await (await Book(Gomez, Juan, "2026-11-23T10:00:00")).Content.ReadFromJsonAsync<AppointmentView>(Json);

        _factory.Time.Advance(TimeSpan.FromMinutes(90)); // 09:30, only 30 minutes before
        var late = await _client.PostAsJsonAsync($"/api/appointments/{booked!.Id}/cancel", new { reason = "Viaje" });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);

        var tomorrow = await (await Book(Gomez, Maria, "2026-11-24T09:00:00")).Content.ReadFromJsonAsync<AppointmentView>(Json);
        var ok = await _client.PostAsJsonAsync($"/api/appointments/{tomorrow!.Id}/cancel", new { reason = "Viaje" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal("Cancelled", (await ok.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        var again = await _client.PostAsJsonAsync($"/api/appointments/{tomorrow.Id}/cancel", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // The cancelled slot can be booked again (the unique index only covers active appointments).
        Assert.Equal(HttpStatusCode.Created, (await Book(Gomez, Juan, "2026-11-24T09:00:00")).StatusCode);
    }

    [Fact]
    public async Task Unknown_doctor_or_patient_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Book(99, Juan, "2026-11-24T10:00:00")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Book(Gomez, 99, "2026-11-24T10:00:00")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/doctors/99/availability?date=2026-11-24")).StatusCode);
    }

    [Fact]
    public async Task Patients_are_validated_and_unique_by_dni()
    {
        var bad = await _client.PostAsJsonAsync("/api/patients", new { fullName = "Al", dni = "12ab", email = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Dni", out _) || errors.TryGetProperty("dni", out _));

        var ok = await _client.PostAsJsonAsync("/api/patients", new { fullName = "Lucía Benítez", dni = "40123456" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var duplicate = await _client.PostAsJsonAsync("/api/patients", new { fullName = "Otra", dni = "40123456" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var found = await _client.GetFromJsonAsync<List<PatientView>>("/api/patients?dni=40123456", Json);
        Assert.Equal("Lucía Benítez", Assert.Single(found!).FullName);
    }

    [Fact]
    public async Task Appointments_can_be_listed_by_doctor_and_day()
    {
        await Book(Gomez, Juan, "2026-11-24T11:00:00");
        await Book(Gomez, Maria, "2026-11-24T09:30:00");
        await Book(Gomez, Juan, "2026-11-25T09:00:00");

        var day = await _client.GetFromJsonAsync<List<AppointmentView>>("/api/appointments?doctorId=1&date=2026-11-24", Json);
        Assert.Equal(["María Fernández", "Juan Pérez"], day!.Select(a => a.Patient.FullName)); // ordered by time
    }
}
