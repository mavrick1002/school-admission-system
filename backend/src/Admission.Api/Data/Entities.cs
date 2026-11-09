namespace Admission.Api.Data;

public static class HoldStatus
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
}

public sealed class SchoolClass
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Hold> Holds { get; } = [];
}

public sealed class Hold
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public required string StudentId { get; set; }
    public required string Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public SchoolClass? Class { get; set; }
}
