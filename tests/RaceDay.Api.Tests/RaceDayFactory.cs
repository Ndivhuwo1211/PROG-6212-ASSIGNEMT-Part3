using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

/// <summary>
/// Boots the real API in memory for tests. SQL Server is swapped for the EF Core in-memory provider
/// and file storage is faked, so the tests need no database, disk or network.
/// Each test class gets its own factory (and therefore its own empty database).
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Registration:OrganiserCode"] = OrganiserCode
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<RaceDayDbContext>>();
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IFileStorageService>();
            services.AddSingleton<IFileStorageService, FakeFileStorageService>();
        });
    }
}

/// <summary>Pretends to store files and returns a predictable URL.</summary>
public class FakeFileStorageService : IFileStorageService
{
    public Task<string> SaveAsync(IFormFile file, string folder, CancellationToken cancellationToken = default) =>
        Task.FromResult($"/uploads/{folder}/test-image.png");
}
