namespace Chat.Api.Helpers;

public static class ChatAttachmentRules
{
    public const string ImageKind = "image";
    public const string VoiceKind = "voice";
    public const string VideoKind = "video";

    public const long MaxImageBytes = 10 * 1024 * 1024;
    public const long MaxVoiceBytes = 15 * 1024 * 1024;
    public const long MaxVideoBytes = 50 * 1024 * 1024;

    public static bool IsSupportedKind(string kind)
        => kind is ImageKind or VoiceKind or VideoKind;

    public static long GetMaxSizeBytes(string kind) => kind switch
    {
        ImageKind => MaxImageBytes,
        VoiceKind => MaxVoiceBytes,
        VideoKind => MaxVideoBytes,
        _ => throw new InvalidOperationException("Unsupported attachment type.")
    };

    public static bool IsSupportedContentType(string kind, string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return kind switch
        {
            ImageKind => contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
            VoiceKind => contentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase),
            VideoKind => contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
