using Remba.Model.Configuration;
using Remba.Service.Users;

namespace Remba.Api.Configuration;

/// <summary>
/// Registers service implementations for dependency injection.
/// Only I*Service → *Service mappings belong here.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceCollection AddRembaServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<UserCacheOptions>(configuration.GetSection(UserCacheOptions.SectionName));
        services.AddMemoryCache();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
