using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

/// <summary>Participants entering events, and Organisers viewing/managing enrolments.</summary>
public class EnrolmentTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EnrolmentTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecordedWithParticipantEventAndCategory()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);

        var response = await EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        var enrolmentId = body.GetProperty("enrolmentId").GetInt32();

        // Verify what was actually persisted in the database.
        var participantId = await GetUserIdAsync(participant);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var saved = await db.Enrolments.SingleAsync(e => e.EnrolmentId == enrolmentId);
        Assert.Equal(participantId, saved.ParticipantId);
        Assert.Equal(eventId, saved.EventId);
        Assert.Equal(categoryId, saved.CategoryId);
    }

    [Fact]
    public async Task Participant_SeesNewEnrolmentInMyEnrolments()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (eventId, categoryId, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await participant.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolments = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        var mine = Assert.Single(enrolments);
        Assert.Equal(enrolmentId, mine.GetProperty("enrolmentId").GetInt32());
        Assert.Equal(eventId, mine.GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, mine.GetProperty("categoryId").GetInt32());
    }

    [Fact]
    public async Task Enrol_SameEventTwice_Returns409()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);
        (await EnrolAsync(participant, eventId, categoryId)).EnsureSuccessStatusCode();

        var second = await EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromDifferentEvent_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventA = await CreateEventAsync(organiser);
        var eventB = await CreateEventAsync(organiser);
        var categoryOfB = await CreateCategoryAsync(organiser, eventB);

        var response = await EnrolAsync(participant, eventA, categoryOfB);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_NonExistentEvent_Returns404()
    {
        var participant = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await EnrolAsync(participant, 999999, 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InEventThatAlreadyHappened_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventId = await CreateEventAsync(organiser, date: DateTime.Now.AddDays(-3));
        var categoryId = await CreateCategoryAsync(organiser, eventId);

        var response = await EnrolAsync(participant, eventId, categoryId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithoutCategoryId_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventId = await CreateEventAsync(organiser);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForOwnEvent()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);
        var p1 = await CreateLoggedInClientAsync(_factory, Participant);
        var p2 = await CreateLoggedInClientAsync(_factory, Participant);
        (await EnrolAsync(p1, eventId, categoryId)).EnsureSuccessStatusCode();
        (await EnrolAsync(p2, eventId, categoryId)).EnsureSuccessStatusCode();

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.ReadJsonAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(owner);

        var response = await other.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_OnlySeesOwnEnrolments()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var alice = await CreateLoggedInClientAsync(_factory, Participant);
        var bob = await CreateLoggedInClientAsync(_factory, Participant);
        await EnrolInNewEventAsync(organiser, alice);

        var response = await bob.GetAsync("/api/users/me/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync()).EnumerateArray());
    }

    [Fact]
    public async Task Organiser_CanConfirmEnrolment()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Confirmed", (await response.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task UpdateEnrolmentStatus_InvalidStatus_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotChangeStatusOfAnotherOrganisersEnrolment_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(owner, participant);

        var response = await other.PutAsJsonAsync($"/api/enrolments/{enrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelOwnEnrolment_Returns204()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, participant);

        var response = await participant.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var mine = await (await participant.GetAsync("/api/users/me/enrolments")).ReadJsonAsync();
        Assert.Empty(mine.EnumerateArray());
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment_Returns403()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var owner = await CreateLoggedInClientAsync(_factory, Participant);
        var intruder = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, _, enrolmentId) = await EnrolInNewEventAsync(organiser, owner);

        var response = await intruder.DeleteAsync($"/api/enrolments/{enrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Event_EnrolmentCount_ReflectsEnrolments()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (eventId, _, _) = await EnrolInNewEventAsync(organiser, participant);

        var detail = await (await _factory.CreateClient().GetAsync($"/api/events/{eventId}")).ReadJsonAsync();

        Assert.Equal(1, detail.GetProperty("enrolmentCount").GetInt32());
    }
}
