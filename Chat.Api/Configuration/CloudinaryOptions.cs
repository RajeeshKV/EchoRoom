namespace Chat.Api.Configuration;

public class CloudinaryOptions
{
    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string UploadFolder { get; set; } = "echoroom/chat";
    public bool Secure { get; set; } = true;
}
