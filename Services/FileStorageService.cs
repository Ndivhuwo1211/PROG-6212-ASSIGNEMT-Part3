namespace RaceDay.Api.Services;

/// <summary>
/// Abstraction over file storage. Part 2 uses local disk (wwwroot/uploads);
/// Part 3 swaps this for an Azure Blob Storage implementation without touching controllers.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Saves the file and returns the public URL/path it can be retrieved from.</summary>
    Task<string> SaveAsync(IFormFile file, string folder, CancellationToken cancellationToken = default);
}

public class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _env;

    public LocalFileStorageService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var targetDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(targetDir);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using var stream = File.Create(Path.Combine(targetDir, fileName));
        await file.CopyToAsync(stream, cancellationToken);

        return $"/uploads/{folder}/{fileName}";
    }
}

/// <summary>Shared validation for image uploads (banner images and profile pictures).</summary>
public static class ImageUploadRules
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "No file was uploaded.";
        if (file.Length > MaxBytes)
            return "The file is too large. The maximum size is 5 MB.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            return "Only JPG, PNG or WEBP images are allowed.";

        return null;
    }
}
