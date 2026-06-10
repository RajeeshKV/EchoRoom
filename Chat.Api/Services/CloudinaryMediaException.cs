using System.Net;

namespace Chat.Api.Services;

public class CloudinaryMediaException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
