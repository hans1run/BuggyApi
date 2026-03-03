using Minio;
using Buggy.API.Services;

namespace Buggy.API.Extensions;

public static class BlobStorageExtensions
{
    public static IServiceCollection AddBlobStorageService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMinioClient>(_ =>
        {
            var endpoint = configuration["Storage:Endpoint"] ?? "localhost:9000";
            var accessKey = configuration["Storage:AccessKey"] ?? "minioadmin";
            var secretKey = configuration["Storage:SecretKey"] ?? "minioadmin123";
            var useSSL = configuration.GetValue<bool>("Storage:UseSSL");

            var client = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSSL)
                .Build();

            return client;
        });

        services.AddSingleton<IBlobStorageService, MinioStorageService>();
        return services;
    }
}
