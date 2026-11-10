using static Admission.Tests.TestApi;

namespace Admission.Tests;

/// <summary>Runs against api-short, whose holds last 2 seconds.</summary>
public sealed class HoldExpiryTests
{
    // Past expiry by enough to absorb scheduling delays under a parallel test load.
    private static Task WaitForExpiryAsync(Reply created) =>
        Task.Delay(TimeSpan.FromSeconds(created.Number("secondsRemaining")) + TimeSpan.FromSeconds(1.5));

    [Fact]
    public async Task An_expired_hold_returns_its_seat_without_any_api_call_and_the_student_can_apply_again()
    {
        var classId = await CreateClassAsync(Short, totalSeats: 1);
        var studentId = NewStudentId();
        var created = await ApplyAsync(Short, classId, studentId);
        Assert.True(created.Status == 201, $"Applying failed: {created}");
        Assert.Equal(2, created.Number("secondsRemaining"));
        Assert.Equal(0, (await GetClassAsync(Short, classId)).Number("availableSeats"));

        await WaitForExpiryAsync(created); // no API calls while the hold runs out

        var classReply = await GetClassAsync(Short, classId);
        Assert.Equal(0, classReply.Number("heldSeats"));
        Assert.Equal(1, classReply.Number("availableSeats"));

        var holdReply = await GetHoldAsync(Short, created.Text("holdId"));
        Assert.Equal("EXPIRED", holdReply.Text("status"));
        Assert.Equal(0, holdReply.Number("secondsRemaining"));

        var again = await ApplyAsync(Short, classId, studentId);
        Assert.True(again.Status == 201, $"Applying again failed: {again}");
        Assert.NotEqual(created.Text("holdId"), again.Text("holdId"));
    }

    [Fact]
    public async Task An_expired_hold_can_be_neither_confirmed_nor_cancelled()
    {
        var classId = await CreateClassAsync(Short, totalSeats: 1);
        var studentId = NewStudentId();
        var created = await ApplyAsync(Short, classId, studentId);
        Assert.True(created.Status == 201, $"Applying failed: {created}");
        var holdId = created.Text("holdId");

        await WaitForExpiryAsync(created);

        var confirm = await ConfirmAsync(Short, holdId, studentId);
        Assert.Equal((409, "HOLD_EXPIRED"), (confirm.Status, confirm.Code));
        var cancel = await CancelAsync(Short, holdId, studentId);
        Assert.Equal((409, "HOLD_EXPIRED"), (cancel.Status, cancel.Code));
        Assert.Equal(0, (await GetClassAsync(Short, classId)).Number("confirmedSeats"));
    }
}
