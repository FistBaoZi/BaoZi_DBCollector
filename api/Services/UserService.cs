using System.Security.Cryptography;
using System.Text;
using MimeKit;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Caching.Memory;

public class UserService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;

    public UserService(IConfiguration configuration, IMemoryCache cache)
    {
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<UserInfo> ValidateUserAsync(string username, string password)
    {
        try
        {
            if (username.ToLower() == "admin" && password == _configuration["Admin"].ToString()) { 
                return new UserInfo() { Username="admin",Id = "1"};
            }
            else
            {
                return null;
            }
        }
        catch (Exception)
        {
            // 记录异常日志
            throw;
        }
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
