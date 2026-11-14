using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database (EF Core Code-First, SQL Server) ---
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Services ---
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

// --- Server-side session (stores the logged-in user's ID and role) ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddControllers();

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "REST API for RaceDay - event management for South African road running, walking and cycling events.\n\n" +
            "**Authentication:** call POST /api/auth/login first. A session cookie is set and Swagger UI sends it " +
            "automatically on later requests. Organiser-only and Participant-only endpoints return 403 for the wrong role " +
            "and 401 when not logged in."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

var app = builder.Build();

// --- Create / migrate the database, then seed demo data (Development only) ---
// Only attempt database creation/migration when running in Development to avoid
// startup failures in environments where the database is not reachable.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

        try
        {
            if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
                db.Database.Migrate();
            else
                db.Database.EnsureCreated();

            DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordService>());
        }
        catch (Exception ex)
        {
            // Log and continue startup. In Development we prefer to surface the
            // error in logs but avoid preventing the app from starting.
            app.Logger.LogError(ex, "Database migration/creation failed during development startup.");
        }
    }
}

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
});

app.UseSession();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Exposes Program to the test project (WebApplicationFactory<Program>).
public partial class Program { }
