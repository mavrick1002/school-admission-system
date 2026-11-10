using static Admission.Tests.TestApi;

namespace Admission.Tests;

public sealed class HoldOwnershipAndStateTests
{
    [Fact]
    public async Task Another_student_can_neither_confirm_nor_cancel_a_hold_and_the_hold_stays_held()
    {
        var classId = await CreateClassAsync(A, totalSeats: 2);
        var owner = NewStudentId();
        var holdId = await ApplyOkAsync(A, classId, owner);
        var stranger = NewStudentId();

        var confirm = await ConfirmAsync(A, holdId, stranger);
        Assert.Equal((403, "NOT_YOUR_HOLD"), (confirm.Status, confirm.Code));
        var cancel = await CancelAsync(B, holdId, stranger);
        Assert.Equal((403, "NOT_YOUR_HOLD"), (cancel.Status, cancel.Code));

        Assert.Equal("HELD", (await GetHoldAsync(A, holdId)).Text("status"));
        var classReply = await GetClassAsync(A, classId);
        Assert.Equal((1, 0), (classReply.Number("heldSeats"), classReply.Number("confirmedSeats")));
    }

    [Fact]
    public async Task A_confirmed_admission_cannot_be_cancelled()
    {
        var classId = await CreateClassAsync(A, totalSeats: 2);
        var studentId = NewStudentId();
        var holdId = await ApplyOkAsync(A, classId, studentId);
        Assert.Equal(200, (await ConfirmAsync(A, holdId, studentId)).Status);

        var cancel = await CancelAsync(B, holdId, studentId);

        Assert.Equal((409, "HOLD_CONFIRMED"), (cancel.Status, cancel.Code));
        Assert.Equal("CONFIRMED", (await GetHoldAsync(A, holdId)).Text("status"));
        Assert.Equal(1, (await GetClassAsync(A, classId)).Number("confirmedSeats"));
    }

    [Fact]
    public async Task Cancelling_a_hold_frees_the_seat_and_the_same_student_can_apply_again()
    {
        var classId = await CreateClassAsync(A, totalSeats: 1);
        var studentId = NewStudentId();
        var holdId = await ApplyOkAsync(A, classId, studentId);
        Assert.Equal(0, (await GetClassAsync(A, classId)).Number("availableSeats"));

        Assert.Equal(204, (await CancelAsync(A, holdId, studentId)).Status);
        Assert.Equal(204, (await CancelAsync(B, holdId, studentId)).Status); // repeat is harmless

        Assert.Equal("CANCELLED", (await GetHoldAsync(A, holdId)).Text("status"));
        Assert.Equal(1, (await GetClassAsync(A, classId)).Number("availableSeats"));

        var again = await ApplyAsync(B, classId, studentId);
        Assert.True(again.Status == 201, $"Applying again failed: {again}");
        Assert.NotEqual(holdId, again.Text("holdId"));

        var confirmOld = await ConfirmAsync(A, holdId, studentId);
        Assert.Equal((409, "HOLD_CANCELLED"), (confirmOld.Status, confirmOld.Code));
    }
}
