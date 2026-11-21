using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Entities;
using Remba.Utility;

namespace Remba.Repository.Entities;

public class EntityRepository : IEntityRepository
{
    private readonly Container _container;
    private readonly ILogger<EntityRepository> _logger;

    public EntityRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<EntityRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.Entities);
    }

    public async Task<EntityDbo?> GetByIdAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<EntityDbo>(entityId, new PartitionKey(entityId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<EntityDbo?> FindByIdentifierAsync(
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(identifierType)
            || StringUtilities.IsNullOrWhiteSpace(identifierValue))
        {
            return null;
        }

        // Bracket-notation required: "value" is reserved in Cosmos SQL.
        const string queryText = """
            SELECT TOP 1 * FROM c
            WHERE EXISTS (
              SELECT VALUE i FROM i IN c.identifiers
              WHERE i["type"] = @type AND i["value"] = @value
            )
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@type", identifierType.Trim())
            .WithParameter("@value", identifierValue.Trim());

        using var iterator = _container.GetItemQueryIterator<EntityDbo>(query);
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

    public async Task<IReadOnlyList<EntityDbo>> SearchAsync(
        string entityType,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalisedType = StringUtilities.NormalizeWhitespace(entityType);
        var normalisedQuery = StringUtilities.NormalizeWhitespace(query);
        if (string.IsNullOrWhiteSpace(normalisedType)
            || string.IsNullOrWhiteSpace(normalisedQuery)
            || limit <= 0)
        {
            return [];
        }

        const string queryText = """
            SELECT TOP @limit * FROM c
            WHERE StringEquals(c["type"], @type, true)
              AND (
                CONTAINS(c["name"], @query, true)
                OR EXISTS (
                  SELECT VALUE i FROM i IN c.identifiers
                  WHERE CONTAINS(i["value"], @query, true)
                )
              )
            """;

        var definition = new QueryDefinition(queryText)
            .WithParameter("@type", normalisedType)
            .WithParameter("@query", normalisedQuery)
            .WithParameter("@limit", Math.Clamp(limit, 1, 25));

        var results = new List<EntityDbo>();
        using var iterator = _container.GetItemQueryIterator<EntityDbo>(definition);
        while (iterator.HasMoreResults && results.Count < limit)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results
            .Take(limit)
            .ToList();
    }

    public async Task<EntityDbo> CreateAsync(EntityDbo entity, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(entity);

        var response = await _container
            .CreateItemAsync(entity, new PartitionKey(entity.Id), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<EntityDbo> UpdateAsync(EntityDbo entity, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(entity);

        var response = await _container
            .ReplaceItemAsync(entity, entity.Id, new PartitionKey(entity.Id), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    private static void ValidateForWrite(EntityDbo entity)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entity.Id))
        {
            throw new ArgumentException("Entity id is required.", nameof(entity));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entity.Name))
        {
            throw new ArgumentException("Entity name is required.", nameof(entity));
        }
    }
}
