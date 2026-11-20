using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Remba.Model.Configuration;
using Remba.Repository.Cosmos;
using Remba.Repository.Entities;
using Remba.Repository.Lists;
using Remba.Repository.System;
using Remba.Repository.Users;

namespace Remba.Api.Configuration;

/// <summary>
/// Registers repository implementations for dependency injection.
/// Only I*Repository → *Repository mappings belong here.
/// </summary>
public static class RepositoryConfiguration
{
    public static IServiceCollection AddRembaRepositories(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cosmos = configuration.GetSection(CosmosDbOptions.SectionName).Get<CosmosDbOptions>()
                     ?? new CosmosDbOptions();

        services.Configure<CosmosDbOptions>(configuration.GetSection(CosmosDbOptions.SectionName));

        if (cosmos.IsConfigured)
        {
            services.AddSingleton(_ =>
            {
                // Document payloads use System.Text.Json. Newtonsoft.Json remains a Cosmos SDK
                // runtime dependency only (internal SDK use), not application serialization.
                var clientOptions = new CosmosClientOptions
                {
                    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    }
                };

                return new CosmosClient(cosmos.Endpoint, cosmos.Key, clientOptions);
            });

            services.AddSingleton<CosmosDbInitializer>();
            services.AddSingleton<IEntityRepository, EntityRepository>();
            services.AddSingleton<IEntityTypeRepository, EntityTypeRepository>();
            services.AddSingleton<IEntryRepository, EntryRepository>();
            services.AddSingleton<IListRepository, ListRepository>();
            services.AddSingleton<ISuggestedListRepository, SuggestedListRepository>();
            services.AddSingleton<IUserRepository, UserRepository>();
        }
        else
        {
            services.AddSingleton<IEntityRepository, InMemoryEntityRepository>();
            services.AddSingleton<IEntityTypeRepository, InMemoryEntityTypeRepository>();
            services.AddSingleton<IEntryRepository, InMemoryEntryRepository>();
            services.AddSingleton<IListRepository, InMemoryListRepository>();
            services.AddSingleton<ISuggestedListRepository, InMemorySuggestedListRepository>();
            services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        }

        return services;
    }
}
