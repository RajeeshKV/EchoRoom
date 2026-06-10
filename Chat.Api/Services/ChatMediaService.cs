using Chat.Api.Configuration;
using Chat.Api.DTOs;
using Chat.Api.Helpers;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Chat.Api.Services;

public class ChatMediaService(
    IOptions<CloudinaryOptions> options,
    ILogger<ChatMediaService> logger) : IChatMediaService, ICloudinaryMediaService
{
    private const string CloudinaryProvider = "cloudinary";
    private readonly CloudinaryOptions _options = options.Value;
    private bool _hasLoggedConfiguration;

    public async Task<ChatAttachmentDto> SaveAsync(IFormFile file, string kind, CancellationToken cancellationToken)
    {
        kind = kind.Trim().ToLowerInvariant();
        ValidateFile(kind, file);

        var resourceType = ResolveUploadResourceType(kind);
        var folder = $"{_options.UploadFolder.Trim('/')}/{kind}";
        var uploadAttemptId = Guid.NewGuid().ToString("N");

        logger.LogInformation(
            "Cloudinary upload starting. AttemptId={AttemptId}, Kind={Kind}, ResourceType={ResourceType}, Folder={Folder}, FileName={FileName}, ContentType={ContentType}, SizeBytes={SizeBytes}, CloudName={CloudName}, ApiKey={ApiKey}, HasApiSecret={HasApiSecret}.",
            uploadAttemptId,
            kind,
            resourceType,
            folder,
            file.FileName,
            file.ContentType,
            file.Length,
            MaskValue(_options.CloudName),
            MaskValue(_options.ApiKey),
            !string.IsNullOrWhiteSpace(_options.ApiSecret));

        logger.LogInformation(
            "Cloudinary SDK upload details. AttemptId={AttemptId}, UploadParams={UploadParams}, ApiKeyLength={ApiKeyLength}, ApiSecretLength={ApiSecretLength}.",
            uploadAttemptId,
            "file,folder,use_filename,unique_filename,overwrite",
            _options.ApiKey.Length,
            _options.ApiSecret.Length);

        await using var fileStream = file.OpenReadStream();
        var cloudinary = CreateCloudinary();
        var uploadResponse = await UploadWithCloudinaryAsync(cloudinary, file, fileStream, kind, folder);

        if (uploadResponse.Error is not null)
        {
            var cloudinaryError = uploadResponse.Error.Message;
            logger.LogWarning(
                "Cloudinary upload failed. AttemptId={AttemptId}, StatusCode={StatusCode}, Error={Error}, DiagnosticHint={DiagnosticHint}.",
                uploadAttemptId,
                (int)uploadResponse.StatusCode,
                cloudinaryError,
                BuildCloudinaryFailureHint(cloudinaryError));
            throw new CloudinaryMediaException(cloudinaryError, uploadResponse.StatusCode);
        }

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
            Url = uploadResponse.SecureUrl?.ToString() ?? uploadResponse.Url?.ToString() ?? string.Empty,
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

        var deleteAttemptId = Guid.NewGuid().ToString("N");

        logger.LogInformation(
            "Cloudinary delete starting. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}, CloudName={CloudName}.",
            deleteAttemptId,
            publicId,
            resourceType,
            MaskValue(_options.CloudName));

        var cloudinary = CreateCloudinary();
        var deleteResponse = await cloudinary.DestroyAsync(new DeletionParams(publicId)
        {
            Invalidate = true,
            ResourceType = ResolveCloudinaryResourceType(resourceType)
        });

        if (deleteResponse.Error is not null)
        {
            var cloudinaryError = deleteResponse.Error.Message;
            logger.LogWarning(
                "Cloudinary delete failed. AttemptId={AttemptId}, PublicId={PublicId}, ResourceType={ResourceType}, StatusCode={StatusCode}, Error={Error}.",
                deleteAttemptId,
                publicId,
                resourceType,
                (int)deleteResponse.StatusCode,
                cloudinaryError);
            throw new CloudinaryMediaException(cloudinaryError, deleteResponse.StatusCode);
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

    private Cloudinary CreateCloudinary()
        => new(new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret));

    private static async Task<RawUploadResult> UploadWithCloudinaryAsync(
        Cloudinary cloudinary,
        IFormFile file,
        Stream fileStream,
        string kind,
        string folder)
    {
        if (kind == ChatAttachmentRules.ImageKind)
        {
            return await cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(file.FileName, fileStream),
                Folder = folder,
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = false
            });
        }

        return await cloudinary.UploadAsync(new VideoUploadParams
        {
            File = new FileDescription(file.FileName, fileStream),
            Folder = folder,
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        });
    }

    private static ResourceType ResolveCloudinaryResourceType(string resourceType)
        => string.Equals(resourceType, "image", StringComparison.OrdinalIgnoreCase)
            ? ResourceType.Image
            : ResourceType.Video;

    private static string BuildCloudinaryFailureHint(string error)
    {
        if (error.Contains("Upload preset must be specified when using unsigned upload", StringComparison.OrdinalIgnoreCase))
        {
            return "Cloudinary did not accept this as a signed upload. Check that api_key and signature multipart fields are present and that ApiSecret belongs to the same CloudName/ApiKey.";
        }

        if (error.Contains("Invalid Signature", StringComparison.OrdinalIgnoreCase))
        {
            return "Cloudinary received a signature but rejected it. Compare signed parameters and ApiSecret for this cloud.";
        }

        return "See Cloudinary response body.";
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

    private sealed record CloudinaryAsset(string PublicId, string ResourceType);
}
