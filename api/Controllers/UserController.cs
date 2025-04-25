using Lazy.Captcha.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class UserController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly UserService _userService;
    private readonly ICaptcha _captcha;

    public UserController(IConfiguration configuration, UserService userService,ICaptcha captcha)
    {
        _configuration = configuration;
        _userService = userService;
        _captcha =  captcha;
    }

    public async Task<IActionResult> Login(LoginModel model)
    {
        try
        {
           var flag =  _captcha.Validate(model.CaptchaId,model.Captcha);
            if (!flag)
            {
                return Content("验证码错误");
            }

            if (string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                return Content("用户名和密码不能为空");
            }

            var user = await _userService.ValidateUserAsync(model.Username, model.Password);
            if (user == null)
            {
                return Content("用户名或密码错误");
            }

            var token = GenerateJwtToken(user.Id);
            return Ok(new { 
                token,
                user = new {
                    id = user.Id,
                    username = user.Username,
                    lastLoginTime = user.LastLoginTime
                }
            });
        }
        catch (Exception ex)
        {
            // 记录异常日志
            return StatusCode(500, new { message = "服务器错误，请稍后重试" });
        }
    }

   
    public IActionResult Captcha(string seed)
    {
        var info = _captcha.Generate(seed);
        using var stream = new MemoryStream(info.Bytes);
        return new FileContentResult(stream.ToArray(), "image/gif");
    }
    


    private string GenerateJwtToken(string username)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: new[] { new Claim(ClaimTypes.Name, username) },
            expires: DateTime.Now.AddMinutes(Convert.ToDouble(jwtSettings["ExpirationInMinutes"])),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginModel
{
    public string Username { get; set; }
    public string Password { get; set; }
    public string Captcha { get; set; }
    public string CaptchaId { get; set; }
}

public class RegisterModel : LoginModel
{
    public string Email { get; set; }
    public string EmailCode { get; set; }
}
