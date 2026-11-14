namespace RaceDay.Api.Tests;

/// <summary>
/// Verifies role-based access: anonymous callers get 401, and the wrong role gets 403,
/// on every Organiser-only and Participant-only endpoint.
/// Authorisation runs before validation/lookup, so the IDs used here do not need to exist.
/// </summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public RoleAccessTests(RaceDayFactory factory) => _factory = factory;

    public static IEnumerable<object[]> OrganiserOnlyEndpoints => new List<object[]>
    {
        new object[] { "POST",   "/api/events" },
        new object[] { "PUT",    "/api/events/1" },
        new object[] { "DELETE", "/api/events/1" },
        new object[] { "GET",    "/api/events/mine" },
        new object[] { "POST",   "/api/events/1/categories" },
        new object[] { "PUT",    "/api/categories/1" },
        new object[] { "DELETE", "/api/categories/1" },
        new object[] { "GET",    "/api/events/1/enrolments" },
        new object[] { "PUT",    "/api/enrolments/1/status" },
        new object[] { "POST",   "/api/enrolments/1/results" },
        new object[] { "PUT",    "/api/results/1" },
        new object[] { "GET",    "/api/events/1/results" },
    };

    public static IEnumerable<object[]> ParticipantOnlyEndpoints => new List<object[]>
    {
        new object[] { "POST",   "/api/events/1/enrolments" },
        new object[] { "GET",    "/api/users/me/enrolments" },
        new object[] { "DELETE", "/api/enrolments/1" },
        new object[] { "GET",    "/api/users/me/results" },
    };

    public static IEnumerable<object[]> AnyLoggedInEndpoints => new List<object[]>
    {
        new object[] { "GET",  "/api/users/me" },
        new object[] { "PUT",  "/api/users/me" },
        new object[] { "POST", "/api/auth/logout" },
    };

    public static IEnumerable<object[]> AllProtectedEndpoints =>
        OrganiserOnlyEndpoints.Concat(ParticipantOnlyEndpoints).Concat(AnyLoggedInEndpoints);

    private static HttpRequestMessage Build(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });
        return request;
    }

    [Theory]
    [MemberData(nameof(OrganiserOnlyEndpoints))]
    public async Task OrganiserOnlyEndpoint_AsParticipant_Returns403(string method, string url)
    {
        var participant = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await participant.SendAsync(Build(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ParticipantOnlyEndpoints))]
    public async Task ParticipantOnlyEndpoint_AsOrganiser_Returns403(string method, string url)
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await organiser.SendAsync(Build(method, url));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AllProtectedEndpoints))]
    public async Task ProtectedEndpoint_WithoutLogin_Returns401(string method, string url)
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.SendAsync(Build(method, url));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/999999")]
    [InlineData("/api/events/999999/categories")]
    public async Task PublicEndpoints_AreAccessibleWithoutLogin(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        // 200 for the list; 404 for non-existent ids. Crucially never 401/403.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_AndNoEventIsCreated()
    {
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var name = $"ShouldNotExist-{Guid.NewGuid():N}";

        var response = await PostEventAsync(participant, name: name);
        var search = await _factory.CreateClient().GetAsync($"/api/events?search={name}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty((await search.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Organiser_CannotEnrolInAnEvent_Returns403()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);

        var response = await EnrolAsync(organiser, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
