using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>Lookup table for user roles (Organiser, Participant).</summary>
public class Role
{
    public int RoleId { get; set; }

    [Required, MaxLength(20)]
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
