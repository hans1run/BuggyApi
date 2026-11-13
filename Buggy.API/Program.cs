using Buggy.API.Data;
using Buggy.API.Extensions;
using Buggy.API.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Buggy.API")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/buggy-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<BuggyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: environment-based
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins("https://buggy.wplan.no")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

// The API is only reachable through Caddy on the Docker network, so trust the
// X-Forwarded-For hop Caddy appends (needed for per-IP login rate limiting).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddBuggyAuthentication(builder.Environment);
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddBlobStorageService(builder.Configuration);
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IBacklogItemService, BacklogItemService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BuggyDbContext>();
    db.Database.Migrate();

    var users = scope.ServiceProvider.GetRequiredService<IUserService>();
    await users.EnsureBootstrapAdminAsync(app.Configuration);
}

// Auto-create MinIO bucket on startup
try
{
    using var scope = app.Services.CreateScope();
    var minio = scope.ServiceProvider.GetRequiredService<IMinioClient>();
    var bucketName = app.Configuration["Storage:BucketName"] ?? "buggy-attachments";
    var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
    if (!exists)
    {
        await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));
        Log.Information("Created MinIO bucket: {BucketName}", bucketName);
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Failed to auto-create MinIO bucket (storage may be unavailable)");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();
