using Microsoft.Extensions.Hosting;
using MMApplication.Application.Interfaces;

namespace MMApplication.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _environment;

    public LocalFileStorageService(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName)
    {
        var rootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
        var targetDirectory = Path.Combine(rootPath, "uploads", folderName);

        if (!Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        var fileExtension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
        var fullPath = Path.Combine(targetDirectory, uniqueFileName);

        using (var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
            await fileStream.CopyToAsync(outputStream);
        }

        // Return relative URL for web access
        return $"/uploads/{folderName}/{uniqueFileName}";
    }

    public Task<bool> DeleteFileAsync(string relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath))
            return Task.FromResult(false);

        var rootPath = Path.Combine(_environment.ContentRootPath, "wwwroot");
        var sanitizedPath = relativeFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(rootPath, sanitizedPath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
