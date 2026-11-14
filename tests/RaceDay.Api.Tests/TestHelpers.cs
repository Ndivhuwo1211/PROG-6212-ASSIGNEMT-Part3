using System.Net.Http.Headers;

namespace RaceDay.Api.Tests;

public static class TestHelpers
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";
    public const string Password = "Passw0rd!123";
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@test.com";

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string role, string email, string password = Password, string? organiserCode = null) =>
        client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = "User",
            email,
            password,
            phoneNumber = "0821234567",
            role,
            organiserCode
        });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    /// <summary>Registers a brand-new user with the given role and returns a client holding their session cookie.</summary>
    public static async Task<HttpClient> CreateLoggedInClientAsync(RaceDayFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail(role.ToLowerInvariant());
        var code = role == Organiser ? OrganiserCode : null;

        (await RegisterAsync(client, role, email, organiserCode: code)).EnsureSuccessStatusCode();
        (await LoginAsync(client, email)).EnsureSuccessStatusCode();
        return client;
    }

    public static async Task<int> GetUserIdAsync(HttpClient loggedInClient)
    {
        var response = await loggedInClient.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("userId").GetInt32();
    }

    public static async Task<HttpResponseMessage> PostEventAsync(
        HttpClient client, DateTime? date = null, string type = "Run", string? name = null) =>
        await client.PostAsJsonAsync("/api/events", new
        {
            name = name ?? $"Test Event {Guid.NewGuid():N}",
            description = "A test event.",
            eventDate = date ?? DateTime.Now.AddDays(30),
            location = "Johannesburg",
            distanceKm = 21.1,
            eventType = type
        });

    public static async Task<int> CreateEventAsync(
        HttpClient organiser, DateTime? date = null, string type = "Run", string? name = null)
    {
        var response = await PostEventAsync(organiser, date, type, name);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("eventId").GetInt32();
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "21km Senior")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name, minAge = 20, maxAge = 39, distanceKm = 21.1 });
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("categoryId").GetInt32();
    }

    public static Task<HttpResponseMessage> EnrolAsync(HttpClient participant, int eventId, int categoryId) =>
        participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

    /// <summary>Creates an event + category, enrols the participant, and returns all the IDs.</summary>
    public static async Task<(int EventId, int CategoryId, int EnrolmentId)> EnrolInNewEventAsync(
        HttpClient organiser, HttpClient participant)
    {
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);
        var response = await EnrolAsync(participant, eventId, categoryId);
        response.EnsureSuccessStatusCode();
        var enrolmentId = (await response.ReadJsonAsync()).GetProperty("enrolmentId").GetInt32();
        return (eventId, categoryId, enrolmentId);
    }

    public static MultipartFormDataContent ImageUpload(string contentType = "image/png", string fileName = "image.png")
    {
        var file = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        return form;
    }
}
