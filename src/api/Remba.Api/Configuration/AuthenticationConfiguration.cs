using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Remba.Api.Auth;
using Remba.Model.Configuration;

namespace Remba.Api.Configuration;

public static class AuthenticationConfiguration
{
    public static IServiceCollection AddRembaAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var auth0 = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>() ?? new Auth0Options();
        services.Configure<Auth0Options>(configuration.GetSection(Auth0Options.SectionName));
        services.AddAuthorization();
        services.AddScoped<CurrentUserResolver>();

        if (!auth0.IsConfigured)
        {
            services.AddAuthentication();
            return services;
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0.Domain}/";
                options.Audience = auth0.Audience;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{auth0.Domain}/",
                    ValidateAudience = true,
                    ValidAudience = auth0.Audience,
                    ValidateLifetime = true,
                    NameClaimType = "name"
                };
            });

        return services;
    }
}
