using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public class EnrolRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A valid categoryId is required.")]
    public int CategoryId { get; set; }
}

public class UpdateEnrolmentStatusRequest
{
    /// <summary>"Pending", "Confirmed" or "Cancelled".</summary>
    [Required]
    public string Status { get; set; } = string.Empty;
}

public record EnrolmentResponse(
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    int CategoryId,
    string CategoryName,
    int ParticipantId,
    string ParticipantName,
    string ParticipantEmail,
    DateTime EnrolmentDate,
    string Status);
