using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds demo data for local development so Swagger can be tried immediately.
/// Passwords are hashed with the same service used by registration.
/// Demo logins:  naledi.khumalo@raceday.co.za / Organiser@123   |   thando.mokoena@example.com / Participant@123
/// </summary>
public static class DbSeeder
{
    public static void Seed(RaceDayDbContext db, IPasswordService passwords)
    {
        if (db.Users.Any())
            return;

        var organiser = new User
        {
            RoleId = 1,
            FirstName = "Naledi",
            LastName = "Khumalo",
            Email = "naledi.khumalo@raceday.co.za",
            PhoneNumber = "0821234567",
            PasswordHash = passwords.Hash("Organiser@123")
        };

        var participant = new User
        {
            RoleId = 2,
            FirstName = "Thando",
            LastName = "Mokoena",
            Email = "thando.mokoena@example.com",
            PhoneNumber = "0731112222",
            PasswordHash = passwords.Hash("Participant@123")
        };

        db.Users.AddRange(organiser, participant);
        db.SaveChanges();

        var halfMarathon = new RaceEvent
        {
            OrganiserId = organiser.UserId,
            Name = "Johannesburg City Half Marathon",
            Description = "A scenic half marathon through the streets of Johannesburg.",
            EventDate = DateTime.Now.AddDays(45),
            Location = "Johannesburg, Gauteng",
            DistanceKm = 21.10m,
            EventType = EventTypes.Run,
            Categories =
            {
                new Category { Name = "21km Senior", MinAge = 20, MaxAge = 39, DistanceKm = 21.10m },
                new Category { Name = "21km Veteran", MinAge = 40, MaxAge = 99, DistanceKm = 21.10m }
            }
        };

        db.Events.Add(halfMarathon);
        db.SaveChanges();
    }
}
