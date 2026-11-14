using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>User registration, login, logout and session behaviour.</summary>
public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public AuthTests(RaceDayFactory factory) => _factory = factory;

    // ---------- Registration ----------

    [Fact]
    public async Task Register_Participant_Returns201_AndDoesNotExposePassword()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("participant");

        var response = await RegisterAsync(client, Participant, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal(Participant, body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var client = _factory.CreateClient();

        var response = await RegisterAsync(client, Organiser, UniqueEmail("organiser"), organiserCode: OrganiserCode);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(Organiser, (await response.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await RegisterAsync(client, Organiser, UniqueEmail("organiser"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_Returns403()
    {
        var client = _factory.CreateClient();

        var response = await RegisterAsync(client, Organiser, UniqueEmail("organiser"), organiserCode: "WRONG-CODE");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("duplicate");
        (await RegisterAsync(client, Participant, email)).EnsureSuccessStatusCode();

        var response = await RegisterAsync(client, Participant, email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await RegisterAsync(_factory.CreateClient(), Participant, "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await RegisterAsync(_factory.CreateClient(), Participant, UniqueEmail("short"), password: "abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400()
    {
        var response = await RegisterAsync(_factory.CreateClient(), "Admin", UniqueEmail("admin"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("hash");
        (await RegisterAsync(client, Participant, email)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash);
        Assert.True(hasher.Verify(Password, user.PasswordHash));
        Assert.False(hasher.Verify("a-different-password", user.PasswordHash));
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_ValidCredentials_Returns200_WithRole()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("login");
        (await RegisterAsync(client, Participant, email)).EnsureSuccessStatusCode();

        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Participant, (await response.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_Organiser_ReturnsOrganiserRole()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("login-org");
        (await RegisterAsync(client, Organiser, email, organiserCode: OrganiserCode)).EnsureSuccessStatusCode();

        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Organiser, (await response.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail("wrongpw");
        (await RegisterAsync(client, Participant, email)).EnsureSuccessStatusCode();

        var response = await LoginAsync(client, email, "TotallyWrong1!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await LoginAsync(_factory.CreateClient(), UniqueEmail("nobody"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Session behaviour ----------

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_Returns200_AndSessionKeepsRole()
    {
        var client = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Participant, (await response.ReadJsonAsync()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Logout_EndsSession_SoProtectedEndpointsReturn401()
    {
        var client = await CreateLoggedInClientAsync(_factory, Participant);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutSession_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Profile ----------

    [Fact]
    public async Task UpdateProfile_ChangesOwnDetails()
    {
        var client = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await client.PutAsJsonAsync("/api/users/me",
            new { firstName = "Thando", lastName = "Mokoena", phoneNumber = "0731112222" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await (await client.GetAsync("/api/users/me")).ReadJsonAsync();
        Assert.Equal("Thando", body.GetProperty("firstName").GetString());
        Assert.Equal("0731112222", body.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task UpdateProfile_WithoutLogin_Returns401()
    {
        var response = await _factory.CreateClient().PutAsJsonAsync("/api/users/me",
            new { firstName = "A", lastName = "B", phoneNumber = "1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadProfilePicture_ValidImage_ReturnsUrl()
    {
        var client = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await client.PostAsync("/api/users/me/profile-picture", ImageUpload());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/uploads/profiles/test-image.png", (await response.ReadJsonAsync()).GetProperty("url").GetString());
    }

    [Fact]
    public async Task UploadProfilePicture_NonImage_Returns400()
    {
        var client = await CreateLoggedInClientAsync(_factory, Participant);

        var response = await client.PostAsync("/api/users/me/profile-picture", ImageUpload("text/plain", "notes.txt"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

/// <summary>Direct unit tests for the password hashing service.</summary>
public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes_BecauseOfSalt()
    {
        Assert.NotEqual(_service.Hash(Password), _service.Hash(Password));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        Assert.True(_service.Verify(Password, _service.Hash(Password)));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        Assert.False(_service.Verify("nope", _service.Hash(Password)));
    }

    [Fact]
    public void Verify_MalformedHash_ReturnsFalse()
    {
        Assert.False(_service.Verify(Password, "not-a-valid-hash"));
    }
}
