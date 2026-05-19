using System.Security.Cryptography;
using System.Text;

namespace Chat.Api.Helpers;

public static class IpHelper
{
    public static string GetIpAddress(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    public static string HashIp(string ipAddress)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexString(hash);
    }
}
