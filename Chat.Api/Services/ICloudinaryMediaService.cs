namespace Chat.Api.Services;

public interface ICloudinaryMediaService
{
    Task DeleteAsync(string publicId, string resourceType, CancellationToken cancellationToken);
}
