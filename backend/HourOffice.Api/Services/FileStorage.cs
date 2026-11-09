using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace HourOffice.Api.Services;

public interface IFileStorage
{
    /// <summary>Stores the file and returns its public URL.</summary>
    Task<string> SaveAsync(Stream content, string extension, string contentType, HttpRequest request, CancellationToken ct);
}

/// <summary>Development storage: files on local disk, served by the API under /uploads.</summary>
public sealed class LocalFileStorage(IOptions<StorageOptions> options, IWebHostEnvironment env) : IFileStorage
{
    public string Root => Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.LocalPath));

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, HttpRequest request, CancellationToken ct)
    {
        Directory.CreateDirectory(Root);
        var name = $"{Guid.NewGuid():N}{extension}";
        await using (var f = File.Create(Path.Combine(Root, name)))
            await content.CopyToAsync(f, ct);
        return $"{request.Scheme}://{request.Host}/uploads/{name}";
    }
}

/// <summary>Production storage on any S3-compatible service (Cloudflare R2 recommended: free egress).</summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly S3StorageOptions _o;
    private readonly AmazonS3Client _client;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        _o = options.Value.S3;
        _client = new AmazonS3Client(_o.AccessKey, _o.SecretKey, new AmazonS3Config
        {
            ServiceURL = _o.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
        });
    }

    public async Task<string> SaveAsync(Stream content, string extension, string contentType, HttpRequest request, CancellationToken ct)
    {
        var key = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _o.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true,
        }, ct);
        return $"{_o.PublicBaseUrl.TrimEnd('/')}/{key}";
    }
}
