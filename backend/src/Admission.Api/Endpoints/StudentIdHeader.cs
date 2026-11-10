using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Admission.Api.Errors;

namespace Admission.Api.Endpoints;

public static partial class StudentIdHeader
{
    public const string Name = "X-Student-Id";

    // [0-9], not \d: in .NET \d also matches non-ASCII digits such as Bengali ones.
    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex Pattern();

    public static bool TryRead(
        HttpRequest request,
        [NotNullWhen(true)] out string? studentId,
        [NotNullWhen(false)] out IResult? error)
    {
        studentId = null;
        error = null;

        var values = request.Headers[Name];
        if (values.Count == 0 || string.IsNullOrEmpty(values[0]))
        {
            error = ApiError.Result(
                StatusCodes.Status400BadRequest,
                "MISSING_STUDENT_ID",
                "Enter the student's birth registration number.");
            return false;
        }

        if (values.Count > 1 || !Pattern().IsMatch(values[0]!))
        {
            error = ApiError.Result(
                StatusCodes.Status400BadRequest,
                "INVALID_STUDENT_ID",
                "The birth registration number must be exactly 17 digits.");
            return false;
        }

        studentId = values[0]!;
        return true;
    }
}
