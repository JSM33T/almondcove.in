using Almondcove.Base.Middlewares;
using Almondcove.Entities.Shared;
using Almondcove.Repositories;
using Almondcove.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

#region Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Async(a => a.File($"Logs/log.txt", rollingInterval: RollingInterval.Hour))
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();
#endregion

#region Fluent Validatoins
builder.Services.AddFluentValidationAutoValidation()
                .AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssembly(Assembly.Load("Almondcove.Validators"));
#endregion

builder.Services.AddControllers();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
}
else
{
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
}

var almondcoveConfigSection = builder.Configuration.GetSection("AlmondcoveConfig");
var almondcoveConfig = builder.Configuration.GetSection("AlmondcoveConfig").Get<AlmondcoveConfig>();

builder.Services.Configure<AlmondcoveConfig>(almondcoveConfigSection);

builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBlogRepository, BlogRepository>();
builder.Services.AddScoped<IMailingService, MailingService>();

#region Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType = ClaimTypes.Role,
        ValidIssuer = almondcoveConfig.JwtSettings.ValidIssuer,
        ValidAudience = almondcoveConfig.JwtSettings.ValidAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(almondcoveConfig.JwtSettings.IssuerSigningKey))
    };
});
#endregion


builder.Services.AddMemoryCache();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});


builder.Services.AddHttpClient();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "keys")))
    .SetApplicationName("AlmondcoveApp");


var rateLimitingOptions = new RateLimitingOptions();
builder.Configuration.GetSection("RateLimiting").Bind(rateLimitingOptions);


#region rateLimiter
builder.Services.AddRateLimiter(options =>
{
    // Apply global rate limiting
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Global.PermitLimit,
                Window = rateLimitingOptions.Global.Window,
                QueueLimit = rateLimitingOptions.Global.QueueLimit,
            });
    });

    // Apply rate limiting for specific routes
    foreach (var route in rateLimitingOptions.Routes)
    {
        options.AddFixedWindowLimiter(route.Key, opt =>
        {
            opt.PermitLimit = route.Value.PermitLimit;
            opt.Window = route.Value.Window;
            opt.QueueLimit = route.Value.QueueLimit;
        });
    }

    options.RejectionStatusCode = 429; // Too Many Requests
});

#endregion

builder.Services.AddCors(o => o.AddPolicy("OpenPolicy", builder =>
{
    builder.AllowAnyOrigin()
           .AllowAnyMethod()
           .AllowAnyHeader();
}));


builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{

}
else
{
    
}

app.UseCors("OpenPolicy");
app.UseHttpsRedirection();
app.UseMiddleware<AcValidationMiddleware>();
app.UseStaticFiles();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
