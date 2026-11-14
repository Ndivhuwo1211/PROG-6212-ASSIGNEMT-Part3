namespace RaceDay.Api.Models;

/// <summary>A participant's finish time/position. Mapped to the "Results" table (1:1 with Enrolments).</summary>
public class RaceResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }

    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int? TotalFinishers { get; set; }
    public DateTime? PublishedAt { get; set; }

    public Enrolment? Enrolment { get; set; }
}
