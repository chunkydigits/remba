using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.System;
using Remba.Utility;

namespace Remba.Repository.System;

public class EntityTypeRepository : IEntityTypeRepository
{
    private readonly Container _container;
    private readonly ILogger<EntityTypeRepository> _logger;

    public EntityTypeRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<EntityTypeRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.System);
    }

    public async Task<EntityTypeDbo?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<EntityTypeDbo>(id, new PartitionKey(id), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == SystemRecordTypes.EntityType
                ? response.Resource
                : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<EntityTypeDbo?> FindByKeyAsync(
        string key,
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        const string queryText = """
            SELECT TOP 1 * FROM c
            WHERE c.recordType = @recordType
              AND c["key"] = @key
              AND (
                c.visibility = @system
                OR c.visibility = @public
                OR (c.visibility = @private AND c.ownerId = @ownerId)
              )
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@recordType", SystemRecordTypes.EntityType)
            .WithParameter("@key", key.Trim().ToLowerInvariant())
            .WithParameter("@system", EntityTypeVisibility.System)
            .WithParameter("@public", EntityTypeVisibility.Public)
            .WithParameter("@private", EntityTypeVisibility.Private)
            .WithParameter("@ownerId", ownerId ?? string.Empty);

        using var iterator = _container.GetItemQueryIterator<EntityTypeDbo>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var match = page.Resource.FirstOrDefault();
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<EntityTypeDbo>> GetAvailableAsync(
        string? ownerId,
        CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT * FROM c
            WHERE c.recordType = @recordType
              AND (
                c.visibility = @system
                OR c.visibility = @public
                OR (c.visibility = @private AND c.ownerId = @ownerId)
              )
            ORDER BY c.name ASC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@recordType", SystemRecordTypes.EntityType)
            .WithParameter("@system", EntityTypeVisibility.System)
            .WithParameter("@public", EntityTypeVisibility.Public)
            .WithParameter("@private", EntityTypeVisibility.Private)
            .WithParameter("@ownerId", ownerId ?? string.Empty);

        var results = new List<EntityTypeDbo>();
        using var iterator = _container.GetItemQueryIterator<EntityTypeDbo>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<EntityTypeDbo> CreateAsync(
        EntityTypeDbo entityType,
        CancellationToken cancellationToken = default)
    {
        ValidateForWrite(entityType);

        var response = await _container
            .CreateItemAsync(
                entityType,
                new PartitionKey(entityType.Id),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    private static void ValidateForWrite(EntityTypeDbo entityType)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entityType.Id))
        {
            throw new ArgumentException("Entity type id is required.", nameof(entityType));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entityType.Key))
        {
            throw new ArgumentException("Entity type key is required.", nameof(entityType));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entityType.Name))
        {
            throw new ArgumentException("Entity type name is required.", nameof(entityType));
        }

        if (!string.Equals(entityType.RecordType, SystemRecordTypes.EntityType, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be entityType.", nameof(entityType));
        }
    }
}
