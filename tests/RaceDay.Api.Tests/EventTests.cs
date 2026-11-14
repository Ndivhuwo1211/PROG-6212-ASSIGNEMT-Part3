namespace RaceDay.Api.Tests;

/// <summary>Event management (Organiser) and event viewing (public), plus categories.</summary>
public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public EventTests(RaceDayFactory factory) => _factory = factory;

    // ---------- Organiser can manage events ----------

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await PostEventAsync(organiser, type: "Cycle");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Cycle", body.GetProperty("eventType").GetString());
        Assert.Equal(21.1m, body.GetProperty("distanceKm").GetDecimal());
    }

    [Fact]
    public async Task CreateEvent_EventTypeIsCaseInsensitive()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await PostEventAsync(organiser, type: "walk");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Walk", (await response.ReadJsonAsync()).GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await PostEventAsync(organiser, type: "Swim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_MissingName_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            description = "No name",
            eventDate = DateTime.Now.AddDays(5),
            location = "Cape Town",
            distanceKm = 10,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_Returns200()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            name = "Renamed Event",
            description = "Updated description",
            eventDate = DateTime.Now.AddDays(60),
            location = "Durban",
            distanceKm = 42.2,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await (await organiser.GetAsync($"/api/events/{eventId}")).ReadJsonAsync();
        Assert.Equal("Renamed Event", body.GetProperty("name").GetString());
        Assert.Equal("Durban", body.GetProperty("location").GetString());
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var otherOrganiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(owner);

        var response = await otherOrganiser.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            name = "Hijacked",
            description = "x",
            eventDate = DateTime.Now.AddDays(10),
            location = "x",
            distanceKm = 5,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_Returns204_ThenEventIsGone()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{eventId}")).StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var otherOrganiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(owner);

        var response = await otherOrganiser.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_WithEnrolments_Returns409()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (eventId, _, _) = await EnrolInNewEventAsync(organiser, participant);

        var response = await organiser.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_MyEvents_OnlyReturnsOwnEvents()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var mine = await CreateEventAsync(organiser);
        var theirs = await CreateEventAsync(other);

        var response = await organiser.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = (await response.ReadJsonAsync()).EnumerateArray().Select(e => e.GetProperty("eventId").GetInt32()).ToList();
        Assert.Contains(mine, ids);
        Assert.DoesNotContain(theirs, ids);
    }

    // ---------- Viewing events (public) ----------

    [Fact]
    public async Task GetEvents_Anonymous_Returns200()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_CanFilterByTypeAndName()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var uniqueName = $"Filter-{Guid.NewGuid():N}";
        var cycleId = await CreateEventAsync(organiser, type: "Cycle", name: uniqueName);
        await CreateEventAsync(organiser, type: "Run", name: $"{uniqueName}-run");

        var response = await _factory.CreateClient().GetAsync($"/api/events?eventType=Cycle&search={uniqueName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        Assert.Single(events);
        Assert.Equal(cycleId, events[0].GetProperty("eventId").GetInt32());
    }

    [Fact]
    public async Task GetEvents_InvalidTypeFilter_Returns400()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events?eventType=Swim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_ExcludesPastEventsUnlessRequested()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var name = $"Past-{Guid.NewGuid():N}";
        var pastId = await CreateEventAsync(organiser, date: DateTime.Now.AddDays(-10), name: name);
        var anon = _factory.CreateClient();

        var upcoming = (await (await anon.GetAsync($"/api/events?search={name}")).ReadJsonAsync()).EnumerateArray().ToList();
        var all = (await (await anon.GetAsync($"/api/events?search={name}&includePast=true")).ReadJsonAsync()).EnumerateArray().ToList();

        Assert.Empty(upcoming);
        Assert.Single(all);
        Assert.Equal(pastId, all[0].GetProperty("eventId").GetInt32());
    }

    [Fact]
    public async Task GetEventById_ReturnsEventWithCategories()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, eventId, "10km Open");

        var response = await _factory.CreateClient().GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = (await response.ReadJsonAsync()).GetProperty("categories").EnumerateArray().ToList();
        Assert.Single(categories);
        Assert.Equal("10km Open", categories[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetEventById_Unknown_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Banner upload ----------

    [Fact]
    public async Task Organiser_CanUploadBanner_ForOwnEvent()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", ImageUpload());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await (await organiser.GetAsync($"/api/events/{eventId}")).ReadJsonAsync();
        Assert.Equal("/uploads/banners/test-image.png", detail.GetProperty("bannerImageUrl").GetString());
    }

    [Fact]
    public async Task UploadBanner_NonImage_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner", ImageUpload("application/pdf", "doc.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var eventId = await CreateEventAsync(organiser);

        var response = await participant.PostAsync($"/api/events/{eventId}/banner", ImageUpload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

/// <summary>Category management (Organiser) and viewing (public).</summary>
public class CategoryTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public CategoryTests(RaceDayFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanAddCategory_AndAnyoneCanListIt()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var create = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Under 20", minAge = 0, maxAge = 19 });
        var list = await _factory.CreateClient().GetAsync($"/api/events/{eventId}/categories");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var categories = (await list.ReadJsonAsync()).EnumerateArray().ToList();
        Assert.Single(categories);
        Assert.Equal("Under 20", categories[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AddCategory_MinAgeGreaterThanMaxAge_Returns400()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "Broken", minAge = 50, maxAge = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddCategory_ForAnotherOrganisersEvent_Returns403()
    {
        var owner = await CreateLoggedInClientAsync(_factory, Organiser);
        var other = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(owner);

        var response = await other.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "Sneaky" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_UnknownEvent_Returns404()
    {
        var response = await _factory.CreateClient().GetAsync("/api/events/999999/categories");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateCategory()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);

        var response = await organiser.PutAsJsonAsync($"/api/categories/{categoryId}",
            new { name = "21km Masters", minAge = 40, maxAge = 99, distanceKm = 21.1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("21km Masters", (await response.ReadJsonAsync()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Organiser_CanDeleteUnusedCategory()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var eventId = await CreateEventAsync(organiser);
        var categoryId = await CreateCategoryAsync(organiser, eventId);

        var response = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithEnrolments_Returns409()
    {
        var organiser = await CreateLoggedInClientAsync(_factory, Organiser);
        var participant = await CreateLoggedInClientAsync(_factory, Participant);
        var (_, categoryId, _) = await EnrolInNewEventAsync(organiser, participant);

        var response = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
