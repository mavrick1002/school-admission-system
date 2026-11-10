using System.Text;
using static Admission.Tests.TestApi;

namespace Admission.Tests;

public sealed class ValidationTests
{
    private const string Valid = "<valid>";
    private const string UnknownId = "00000000-0000-0000-0000-000000000000";

    // rule, method, path ({class} and {hold} are filled in per case), X-Student-Id
    // (null = no header, <valid> = a fresh valid id), JSON body, expected status and code.
    public static TheoryData<string, string, string, string?, string?, int, string> Cases => new()
    {
        { "apply without X-Student-Id", "POST", "/classes/{class}/holds", null, null, 400, "MISSING_STUDENT_ID" },
        { "apply with an empty X-Student-Id", "POST", "/classes/{class}/holds", "", null, 400, "MISSING_STUDENT_ID" },
        { "apply with 16 digits", "POST", "/classes/{class}/holds", "2015269123456789", null, 400, "INVALID_STUDENT_ID" },
        { "apply with 18 digits", "POST", "/classes/{class}/holds", "201526912345678901", null, 400, "INVALID_STUDENT_ID" },
        { "apply with a letter", "POST", "/classes/{class}/holds", "2015269123456789A", null, 400, "INVALID_STUDENT_ID" },
        { "apply with Bengali digits", "POST", "/classes/{class}/holds", "২০১৫২৬৯১২৩৪৫৬৭৮৯", null, 400, "INVALID_STUDENT_ID" },
        { "confirm without X-Student-Id", "POST", "/holds/{hold}/confirm", null, null, 400, "MISSING_STUDENT_ID" },
        { "confirm with an invalid X-Student-Id", "POST", "/holds/{hold}/confirm", "12345", null, 400, "INVALID_STUDENT_ID" },
        { "cancel without X-Student-Id", "DELETE", "/holds/{hold}", null, null, 400, "MISSING_STUDENT_ID" },
        { "cancel with an invalid X-Student-Id", "DELETE", "/holds/{hold}", "12345", null, 400, "INVALID_STUDENT_ID" },
        { "list my holds without X-Student-Id", "GET", "/me/holds", null, null, 400, "MISSING_STUDENT_ID" },
        { "list my holds with an invalid X-Student-Id", "GET", "/me/holds", "12345", null, 400, "INVALID_STUDENT_ID" },

        { "create a class with an empty name", "POST", "/classes", null, """{"name":"","totalSeats":10}""", 400, "VALIDATION_ERROR" },
        { "create a class with a blank name", "POST", "/classes", null, """{"name":"   ","totalSeats":10}""", 400, "VALIDATION_ERROR" },
        { "create a class without a name", "POST", "/classes", null, """{"totalSeats":10}""", 400, "VALIDATION_ERROR" },
        { "create a class with totalSeats 0", "POST", "/classes", null, """{"name":"Class 6","totalSeats":0}""", 400, "VALIDATION_ERROR" },
        { "create a class with negative totalSeats", "POST", "/classes", null, """{"name":"Class 6","totalSeats":-1}""", 400, "VALIDATION_ERROR" },
        { "create a class without totalSeats", "POST", "/classes", null, """{"name":"Class 6"}""", 400, "VALIDATION_ERROR" },
        { "create a class with malformed JSON", "POST", "/classes", null, """{"name":"Class 6",""", 400, "INVALID_JSON" },
        { "create a class with totalSeats as a string", "POST", "/classes", null, """{"name":"Class 6","totalSeats":"10"}""", 400, "INVALID_JSON" },

        { "get an unknown class", "GET", $"/classes/{UnknownId}", null, null, 404, "CLASS_NOT_FOUND" },
        { "get a class by a non-GUID id", "GET", "/classes/c_123", null, null, 404, "CLASS_NOT_FOUND" },
        { "apply to an unknown class", "POST", $"/classes/{UnknownId}/holds", Valid, null, 404, "CLASS_NOT_FOUND" },
        { "apply to a non-GUID class id", "POST", "/classes/c_123/holds", Valid, null, 404, "CLASS_NOT_FOUND" },
        { "get an unknown hold", "GET", $"/holds/{UnknownId}", null, null, 404, "HOLD_NOT_FOUND" },
        { "get a hold by a non-GUID id", "GET", "/holds/h_789", null, null, 404, "HOLD_NOT_FOUND" },
        { "confirm an unknown hold", "POST", $"/holds/{UnknownId}/confirm", Valid, null, 404, "HOLD_NOT_FOUND" },
        { "confirm a non-GUID hold id", "POST", "/holds/h_789/confirm", Valid, null, 404, "HOLD_NOT_FOUND" },
        { "cancel an unknown hold", "DELETE", $"/holds/{UnknownId}", Valid, null, 404, "HOLD_NOT_FOUND" },
        { "cancel a non-GUID hold id", "DELETE", "/holds/h_789", Valid, null, 404, "HOLD_NOT_FOUND" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Invalid_requests_are_rejected_with_a_json_error_code(
        string rule, string method, string path, string? studentId, string? json, int status, string code)
    {
        // Each case gets its own class and hold, so the bad request has real targets.
        var classId = await CreateClassAsync(A, totalSeats: 3);
        var holdId = await ApplyOkAsync(A, classId, NewStudentId());

        var url = A + path.Replace("{class}", classId).Replace("{hold}", holdId);
        var content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json");
        var reply = await SendAsync(
            new HttpMethod(method), url, studentId == Valid ? NewStudentId() : studentId, content);

        Assert.True(
            reply.Status == status && reply.Code == code,
            $"{rule}: expected {status} {code}, got {reply}");
        Assert.False(string.IsNullOrWhiteSpace(reply.Text("message")), $"{rule}: no message in {reply}");

        // A rejected request must not have changed the class.
        var classReply = await GetClassAsync(A, classId);
        Assert.Equal((1, 0), (classReply.Number("heldSeats"), classReply.Number("confirmedSeats")));
    }
}
