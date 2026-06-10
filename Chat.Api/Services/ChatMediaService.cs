using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chat.Api.Configuration;
using Chat.Api.DTOs;
using Chat.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Chat.Api.Services;

public class ChatMediaService(
    HttpClient httpClient,
    IOptions<CloudinaryOptions> options,
    ILogger<ChatMediaService> logger) : IChatMediaService, ICloudinaryMediaService
{
    private const string CloudinaryProvider = "cloudinary";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CloudinaryOptions _options = options.Value;
    private bool _hasLoggedConfiguration;

    public async Task<ChatAttachmentDto> SaveAsync(IFormFile file, string kind, CancellationToken cancellationToken)
    {
        kind = kind.Trim().ToLowerInvariant();
        ValidateFile(kind, file);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resourceType = ResolveUploadResourceType(kind);
        var folder = $"{_options.UploadFolder.Trim('/')}/{kind}";
        var uploadAttemptId = Guid.NewGuid().ToString("N");
        var signature = SignParameters(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["folder"] = folder,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture)
        });

        logger.LogInformation(
            "Cloudinary upload starting. AttemptId={AttemptId}, Kind={Kind}, ResourceType={ResourceType}, Folder={Folder}, FileName={FileName}, ContentType={ContentType}, SizeBytes={SizeBytes}, CloudName={CloudName}, HasApiKey={HasApiKey}, HasApiSecret={HasApiSecret}.",
            uploadAttemptId,
            kind,
            resourceType,
            folder,
            file.FileName,
            file.ContentType,
            file.Length,
            MaskValue(_options.CloudName),
            !string.IsNullOrWhiteSpace(_options.ApiKey),
            !string.IsNullOrWhiteSpace(_options.ApiSecret));

        using var formData = new MultipartFormDataContent();
        await using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);

        formData.Add(streamContent, "file", file.FileName);
        formData.Add(new StringContent(_options.ApiKey), "api_key");
        formData.Add(new StringContent(folder), "folder");
        formData.Add(new StringContent(signature), "signature");
        formData.Add(new StringContent(timestamp.ToString(CultureInfo.InvariantCulture)), "timestamp");

        var response = await httpClient.PostAsync(BuildUploadUrl(resourceType), formData, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var cloudinaryError = ReadCloudinaryError(payload);
            logger.LogWarning(
                "Cloudinary upload failed. AttemptId={AttemptId}, StatusCode={StatusCode}, Error={Error}, ResponseBody={ResponseBody}.",
                uploadAttemptId,
                (int)response.StatusCode,
                cloudinaryError,
                TruncateForLog(payload));
            throw new CloudinaryMediaException(cloudinaryError, response.StatusCode);
        }

        var uploadResponse = JsonSerializer.Deserialize<CloudinaryUploadResponse>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Cloudinary upload response was empty.");

        logger.LogInformation(
            "Cloudinary upload succeeded. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}, Bytes={Bytes}.",
            uploadAttemptId,
            uploadResponse.PublicId,
            uploadResponse.ResourceType,
            uploadResponse.Bytes);

        return new ChatAttachmentDto
        {
            Provider = CloudinaryProvider,
            Kind = kind,
            Url = uploadResponse.SecureUrl,
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            PublicId = uploadResponse.PublicId,
            ResourceType = uploadResponse.ResourceType
        };
    }

    public Task<ChatAttachmentDto> ValidateAsync(ChatAttachmentDto attachment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var kind = attachment.Kind.Trim().ToLowerInvariant();
        if (!ChatAttachmentRules.IsSupportedKind(kind))
        {
            throw new InvalidOperationException("Unsupported attachment type.");
        }

        if (!ChatAttachmentRules.IsSupportedContentType(kind, attachment.ContentType))
        {
            throw new InvalidOperationException($"Attachment content type is invalid for {kind}.");
        }

        if (attachment.SizeBytes <= 0 || attachment.SizeBytes > ChatAttachmentRules.GetMaxSizeBytes(kind))
        {
            throw new InvalidOperationException($"Attachment size is invalid for {kind}.");
        }

        var cloudinaryAsset = ParseCloudinaryAsset(attachment.Url);

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ChatAttachmentDto
        {
            Provider = CloudinaryProvider,
            Kind = kind,
            Url = attachment.Url,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            PublicId = cloudinaryAsset.PublicId,
            ResourceType = cloudinaryAsset.ResourceType
        });
    }

    public async Task DeleteAsync(string publicId, string resourceType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId) || string.IsNullOrWhiteSpace(resourceType))
        {
            return;
        }

        ValidateConfigured();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deleteAttemptId = Guid.NewGuid().ToString("N");
        var signature = SignParameters(new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture)
        });

        logger.LogInformation(
            "Cloudinary delete starting. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}, CloudName={CloudName}.",
            deleteAttemptId,
            publicId,
            resourceType,
            MaskValue(_options.CloudName));

        using var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = _options.ApiKey,
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["signature"] = signature,
            ["timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture)
        });

        var response = await httpClient.PostAsync(BuildDestroyUrl(resourceType), formData, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var cloudinaryError = ReadCloudinaryError(payload);
            logger.LogWarning(
                "Cloudinary delete failed. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}, StatusCode={StatusCode}, Error={Error}, ResponseBody={ResponseBody}.",
                deleteAttemptId,
                publicId,
                resourceType,
                (int)response.StatusCode,
                cloudinaryError,
                TruncateForLog(payload));
            throw new CloudinaryMediaException(cloudinaryError, response.StatusCode);
        }

        logger.LogInformation(
            "Cloudinary delete succeeded. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}.",
            deleteAttemptId,
            publicId,
            resourceType);
    }

    private void ValidateFile(string kind, IFormFile file)
    {
        if (!ChatAttachmentRules.IsSupportedKind(kind))
        {
            throw new InvalidOperationException("Unsupported attachment type.");
        }

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        var maxSizeBytes = ChatAttachmentRules.GetMaxSizeBytes(kind);
        if (file.Length > maxSizeBytes)
        {
            throw new InvalidOperationException($"The {kind} file exceeds the allowed limit of {maxSizeBytes / (1024 * 1024)} MB.");
        }

        if (!ChatAttachmentRules.IsSupportedContentType(kind, file.ContentType))
        {
            throw new InvalidOperationException($"The uploaded file is not a valid {kind}.");
        }

        ValidateConfigured();
    }

    private void ValidateConfigured()
    {
        if (!_hasLoggedConfiguration)
        {
            logger.LogInformation(
                "Cloudinary configuration present: CloudName={HasCloudName}, ApiKey={HasApiKey}, ApiSecret={HasApiSecret}, UploadFolder={UploadFolder}.",
                !string.IsNullOrWhiteSpace(_options.CloudName),
                !string.IsNullOrWhiteSpace(_options.ApiKey),
                !string.IsNullOrWhiteSpace(_options.ApiSecret),
                _options.UploadFolder);
            _hasLoggedConfiguration = true;
        }

        if (string.IsNullOrWhiteSpace(_options.CloudName) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary configuration is incomplete.");
        }
    }

    private string BuildUploadUrl(string resourceType)
        => $"https://api.cloudinary.com/v1_1/{_options.CloudName}/{resourceType}/upload";

    private string BuildDestroyUrl(string resourceType)
        => $"https://api.cloudinary.com/v1_1/{_options.CloudName}/{resourceType}/destroy";

    private CloudinaryAsset ParseCloudinaryAsset(string url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "res.cloudinary.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment URL must be a valid Cloudinary URL.");
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length < 5 ||
            !string.Equals(segments[0], _options.CloudName, StringComparison.Ordinal) ||
            !string.Equals(segments[2], "upload", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Attachment URL must be a valid Cloudinary URL.");
        }

        var resourceType = segments[1];
        var versionIndex = Array.FindIndex(
            segments,
            3,
            x => x.Length > 1 && x[0] == 'v' && x[1..].All(char.IsDigit));

        if (versionIndex < 0 || versionIndex == segments.Length - 1)
        {
            throw new InvalidOperationException("Attachment URL must include a Cloudinary public ID.");
        }

        var publicIdSegments = segments[(versionIndex + 1)..];
        publicIdSegments[^1] = Path.GetFileNameWithoutExtension(publicIdSegments[^1]);
        var publicId = string.Join("/", publicIdSegments);

        if (string.IsNullOrWhiteSpace(publicId))
        {
            throw new InvalidOperationException("Attachment URL must include a Cloudinary public ID.");
        }

        return new CloudinaryAsset(publicId, resourceType);
    }

    private string ResolveUploadResourceType(string kind) => kind switch
    {
        ChatAttachmentRules.ImageKind => "image",
        ChatAttachmentRules.VoiceKind => "video",
        ChatAttachmentRules.VideoKind => "video",
        _ => "auto"
    };

    private string SignParameters(SortedDictionary<string, string> parameters)
    {
        var joined = string.Join("&", parameters.Select(x => $"{x.Key}={x.Value}"));
        var toHash = $"{joined}{_options.ApiSecret}";
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(toHash));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ReadCloudinaryError(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "Cloudinary rejected the media request.";
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }
        }
        catch (JsonException)
        {
            return "Cloudinary rejected the media request.";
        }

        return "Cloudinary rejected the media request.";
    }

    private static string MaskValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value.Length <= 4
            ? "****"
            : $"{value[..2]}***{value[^2..]}";
    }

    private static string TruncateForLog(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        return value.Length <= 500 ? value : $"{value[..500]}...";
    }

    private sealed record CloudinaryAsset(string PublicId, string ResourceType);

    private sealed class CloudinaryUploadResponse
    {
        [JsonPropertyName("public_id")]
        public string PublicId { get; set; } = string.Empty;

        [JsonPropertyName("resource_type")]
        public string ResourceType { get; set; } = string.Empty;

        [JsonPropertyName("secure_url")]
        public string SecureUrl { get; set; } = string.Empty;

        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }
    }
}
