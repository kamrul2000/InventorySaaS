using InventorySaaS.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace InventorySaaS.Infrastructure.Services.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public LocalFileStorageService(IConfiguration configuration)
    {
        _basePath = configuration["FileStorage:BasePath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _baseUrl = configuration["FileStorage:BaseUrl"] ?? "/uploads";
    }

    public async Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var directoryPath = Path.Combine(_basePath, containerName);
        Directory.CreateDirectory(directoryPath);

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(directoryPath, uniqueFileName);

        await using var fileStream = new FileStream(filePath, FileMode.Create);
        await content.CopyToAsync(fileStream, ct);

        return $"{_baseUrl}/{containerName}/{uniqueFileName}";
    }

    public async Task<Stream?> DownloadAsync(string containerName, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, containerName, fileName);
        if (!File.Exists(filePath)) return null;

        var memory = new MemoryStream();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await stream.CopyToAsync(memory, ct);
        memory.Position = 0;
        return memory;
    }

    public Task DeleteAsync(string containerName, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, containerName, fileName);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public string GetUrl(string containerName, string fileName)
    {
        return $"{_baseUrl}/{containerName}/{fileName}";
    }
}
