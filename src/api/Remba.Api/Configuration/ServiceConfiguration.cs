using Remba.Model.Configuration;
using Remba.Service.Entities;
using Remba.Service.Lists;
using Remba.Service.System;
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
        services.Configure<OmdbOptions>(configuration.GetSection(OmdbOptions.SectionName));
        services.Configure<TmdbOptions>(configuration.GetSection(TmdbOptions.SectionName));
        services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.SectionName));
        services.Configure<FlagdexOptions>(configuration.GetSection(FlagdexOptions.SectionName));
        services.AddMemoryCache();

        var omdb = configuration.GetSection(OmdbOptions.SectionName).Get<OmdbOptions>() ?? new OmdbOptions();
        var tmdb = configuration.GetSection(TmdbOptions.SectionName).Get<TmdbOptions>() ?? new TmdbOptions();
        var openLibrary = configuration.GetSection(OpenLibraryOptions.SectionName).Get<OpenLibraryOptions>()
                          ?? new OpenLibraryOptions();
        var flagdex = configuration.GetSection(FlagdexOptions.SectionName).Get<FlagdexOptions>()
                      ?? new FlagdexOptions();

        services.AddHttpClient<OmdbLookupProvider>(client =>
        {
            client.BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(omdb.BaseUrl) ? "https://www.omdbapi.com/" : omdb.BaseUrl);
        });
        services.AddHttpClient<TmdbWatchProviderService>(client =>
        {
            client.BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(tmdb.BaseUrl) ? "https://api.themoviedb.org/3/" : tmdb.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Remba/1.0 (watch-providers)");
        });
        services.AddTransient<ITmdbWatchProviderService>(sp => sp.GetRequiredService<TmdbWatchProviderService>());
        services.AddHttpClient<OpenLibraryLookupProvider>(client =>
        {
            client.BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(openLibrary.BaseUrl)
                    ? "https://openlibrary.org/"
                    : openLibrary.BaseUrl);
            // Open Library asks callers to identify themselves.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Remba/1.0 (local-dev; books-search)");
        });
        services.AddHttpClient<CountryLookupProvider>(client =>
        {
            client.BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(flagdex.BaseUrl) ? "https://flagdex.net/" : flagdex.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Remba/1.0 (local-dev; country-search)");
        });

        services.AddTransient<IEntityLookupProvider>(sp => sp.GetRequiredService<OmdbLookupProvider>());
        services.AddTransient<IEntityLookupProvider>(sp => sp.GetRequiredService<OpenLibraryLookupProvider>());
        services.AddTransient<IEntityLookupProvider>(sp => sp.GetRequiredService<CountryLookupProvider>());

        services.AddScoped<IEntityLookupService, EntityLookupService>();
        services.AddScoped<IEntityTypeService, EntityTypeService>();
        services.AddScoped<IEntryService, EntryService>();
        services.AddScoped<IListService, ListService>();
        services.AddScoped<IListShareService, ListShareService>();
        services.AddScoped<IListInviteService, ListInviteService>();
        services.AddScoped<IShareGroupService, ShareGroupService>();
        services.AddScoped<ISuggestedListService, SuggestedListService>();
        services.AddScoped<IUserService, UserService>();
        return services;
    }
}
