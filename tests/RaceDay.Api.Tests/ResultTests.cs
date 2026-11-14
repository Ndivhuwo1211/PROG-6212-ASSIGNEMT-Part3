namespace RaceDay.Api.Tests;

/// <summary>Organisers capturing finish times/positions; Participants viewing their own results.</summary>
public class ResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public ResultTests(RaceDayFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> CaptureResultAsync(
        HttpClient organiser, int enrolmentId, string finishTime = "01:45:32", int position = 47, int total = 312) =>
        organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/results",
            new { finishTime, finishPosition = position, totalFinishers = total });

    [Fact]
    public async Task Organiser_CanCaptureResult_Returns201()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await CaptureResultAsync(organiser, enrolmentId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("01:45:32", body.GetProperty("finishTime").GetString());
        Assert.Equal(47, body.GetProperty("finishPosition").GetInt32());
        Assert.Equal(312, body.GetProperty("totalFinishers").GetInt32());
    }

    [Fact]
    public async Task Participant_SeesOwnResult_WithEventCategoryTimeAndPosition()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (eventId, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);
        (await CaptureResultAsync(organiser, enrolmentId)).EnsureSuccessStatusCode();

        var response = await participant.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        var mine = Assert.Single(results);
        Assert.Equal(eventId, mine.GetProperty("eventId").GetInt32());
        Assert.Equal("21km Senior", mine.GetProperty("categoryName").GetString());
        Assert.Equal("01:45:32", mine.GetProperty("finishTime").GetString());
        Assert.Equal(47, mine.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Participant_CannotSeeAnotherParticipantsResults()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var alice = await CreateLoggedInClientAsync(_factory, Participant);
        var bob = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, aliceEnrolment) = await EnrolInNewEventAsync(organiser, alice);
        (await CaptureResultAsync(organiser, aliceEnrolment)).EnsureSuccessStatusCode();

        var response = await bob.GetAsync("/api/users/me/results");

        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);
        (await CaptureResultAsync(organiser, enrolmentId)).EnsureSuccessStatusCode();

        var second = await CaptureResultAsync(organiser, enrolmentId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_PositionGreaterThanTotalFinishers_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await CaptureResultAsync(organiser, enrolmentId, position: 500, total: 312);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ZeroFinishTime_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await CaptureResultAsync(organiser, enrolmentId, finishTime: "00:00:00");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(owner, participant);

        var response = await CaptureResultAsync(other, enrolmentId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_UnknownEnrolment_Returns404()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await CaptureResultAsync(organiser, 999999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await CaptureResultAsync(participant, enrolmentId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateResult()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);
        var created = await (await CaptureResultAsync(organiser, enrolmentId)).ReadJsonAsync();
        var resultId = created.GetProperty("resultId").GetInt32();

        var response = await organiser.PutAsJsonAsync($"/api/results/{resultId}",
            new { finishTime = "01:40:00", finishPosition = 12, totalFinishers = 312 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("01:40:00", body.GetProperty("finishTime").GetString());
        Assert.Equal(12, body.GetProperty("finishPosition").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanViewResultsForOwnEvent_OrderedByPosition()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);
        var slow = await CreateLoggedInClientAsync(_factory, Participant);
        var fast = await CreateLoggedInClientAsync(_factory, Participant);
        var slowEnrolment = (await (await EnrolAsync(slow, eventId, categoryId)).ReadJsonAsync()).GetProperty("enrolmentId").GetInt32();
        var fastEnrolment = (await (await EnrolAsync(fast, eventId, categoryId)).ReadJsonAsync()).GetProperty("enrolmentId").GetInt32();
        (await CaptureResultAsync(organiser, slowEnrolment, "02:10:00", position: 2, total: 2)).EnsureSuccessStatusCode();
        (await CaptureResultAsync(organiser, fastEnrolment, "01:30:00", position: 1, total: 2)).EnsureSuccessStatusCode();

        var response = await organiser.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var positions = (await response.ReadJsonAsync()).EnumerateArray()
            .Select(r => r.GetProperty("finishPosition").GetInt32()).ToList();
        Assert.Equal(new[] { 1, 2 }, positions);
    }

    [Fact]
    public async Task Organiser_CannotViewResultsForAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(owner);

        var response = await other.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancelEnrolment_OnceResultExists_Returns409()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);
        (await CaptureResultAsync(organiser, enrolmentId)).EnsureSuccessStatusCode();

        var response = await participant.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
