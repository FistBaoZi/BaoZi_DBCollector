using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Lazy.Captcha.Core.Generator;
using Lazy.Captcha.Core.Generator.Image.Option;
using SkiaSharp;
using BaoZi.Tools.DBHelper;
using Microsoft.Extensions.DependencyInjection;
using dbcollector_api.Services;
using BaoZi.Tools.WebApiInit;
using BaoZi.Tools.TimerTask;


var builder = WebApplicationFactory.CreateBuilderWithWindowService();

builder.Services.AddMemoryCache(); // 添加这一行来注册 IMemoryCache
builder.Services.AddControllers();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<DBCollectorService>();
var dbcenter = new SqlServerDBHelper(builder.Configuration.GetConnectionString("DataCenter"), "DataCenter");
builder.Services.AddSingleton<IBaseDBHelper>(dbcenter);
TimerTaskService timerTaskService = new TimerTaskService();
builder.Services.AddSingleton(timerTaskService);



// 替换原有的 JWT 配置
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero, // 可选：减少令牌过期的时间偏差
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCaptcha(x => {
    x.CaptchaType = CaptchaType.WORD;
    x.CodeLength = 4;
    x.ExpirySeconds = 60;
    x.IgnoreCase = true;
    x.ImageOption = new CaptchaImageGeneratorOption()
    {
        Animation = false,
        FontSize = 32,
        Width = 100,
        Height = 40,
        BubbleMinRadius = 2,
        BubbleMaxRadius = 5,
        BubbleCount = 2,
        BubbleThickness = 1.0f,
        InterferenceLineCount = 4,
        Quality = 100,
        ForegroundColors = new List<SKColor> { SKColor.FromHsl(0, 0, 0) }
    };
});

var app = builder.Build();

// 初始化数据库表
var dbCollectorService = app.Services.GetService<DBCollectorService>();
await dbCollectorService.EnsureStationTableExistsAsync();

//初始化采集定时任务
await dbCollectorService.InitCTTimerTask();
app.UseStaticFiles();
app.MapControllers();
//配置路由
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("@烟神殿路由", "/{controller}/{action}/{id?}");


// 配置SPA fallback
app.MapFallbackToFile("index.html");

app.Run();
