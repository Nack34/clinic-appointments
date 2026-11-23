using Clinic.Api.Domain;
using Clinic.Api.Scheduling;

namespace Clinic.Tests;

public class SlotTests
{
    private static Doctor DoctorWith(int slotMinutes, params (int start, int end)[] shifts) => new()
    {
        FullName = "Test", Specialty = "Test", SlotMinutes = slotMinutes,
        WorkingHours = [.. shifts.Select(s => new WorkingHours
        {
            Day = DayOfWeek.Wednesday, Start = new TimeOnly(s.start, 0), End = new TimeOnly(s.end, 0),
        })],
    };

    private static readonly DateOnly Wednesday = new(2026, 11, 25);

    [Fact]
    public void Slots_that_do_not_fit_at_the_end_of_a_shift_are_dropped()
    {
        var slots = SchedulingService.AllSlots(DoctorWith(40, (10, 12)), Wednesday).ToList();
        Assert.Equal(3, slots.Count); // 10:00, 10:40, 11:20 (12:00 would end at 12:40)
        Assert.Equal(new TimeOnly(11, 20), TimeOnly.FromDateTime(slots[^1].StartsAt));
    }

    [Fact]
    public void Shifts_are_returned_in_order_even_if_stored_out_of_order()
    {
        var slots = SchedulingService.AllSlots(DoctorWith(60, (15, 16), (9, 10)), Wednesday).ToList();
        Assert.Equal([9, 15], slots.Select(s => s.StartsAt.Hour));
    }

    [Fact]
    public void No_slots_on_days_without_shifts()
    {
        Assert.Empty(SchedulingService.AllSlots(DoctorWith(30, (9, 12)), Wednesday.AddDays(1)));
    }
}
