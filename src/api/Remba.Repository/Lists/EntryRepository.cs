using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class EntryRepository : IEntryRepository
{
    private readonly Container _container;
    private readonly ILogger<EntryRepository> _logger;

    public EntryRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<EntryRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.UserLists);
    }

    public async Task<EntryDbo?> GetByIdAsync(string ownerId, string entryId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId) || StringUtilities.IsNullOrWhiteSpace(entryId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<EntryDbo>(entryId, new PartitionKey(ownerId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == ListRecordTypes.Entry ? response.Resource : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<EntryDbo>> GetByListAsync(
        string ownerId,
        string listId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId) || StringUtilities.IsNullOrWhiteSpace(listId))
        {
            return [];
        }

        var queryText = includeDeleted
            ? """
              SELECT * FROM c
              WHERE c.ownerId = @ownerId
                AND c.listId = @listId
                AND c.recordType = @recordType
              ORDER BY c["order"] ASC
              """
            : """
              SELECT * FROM c
              WHERE c.ownerId = @ownerId
                AND c.listId = @listId
                AND c.recordType = @recordType
                AND (NOT IS_DEFINED(c.deleted) OR IS_NULL(c.deleted))
              ORDER BY c["order"] ASC
              """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", ownerId)
            .WithParameter("@listId", listId)
            .WithParameter("@recordType", ListRecordTypes.Entry);

        var results = new List<EntryDbo>();
        using var iterator = _container.GetItemQueryIterator<EntryDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<EntryDbo?> FindActiveByEntityAsync(
        string ownerId,
        string listId,
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId)
            || StringUtilities.IsNullOrWhiteSpace(listId)
            || StringUtilities.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        const string queryText = """
            SELECT TOP 1 * FROM c
            WHERE c.ownerId = @ownerId
              AND c.listId = @listId
              AND c.entityId = @entityId
              AND c.recordType = @recordType
              AND (NOT IS_DEFINED(c.deleted) OR IS_NULL(c.deleted))
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", ownerId)
            .WithParameter("@listId", listId)
            .WithParameter("@entityId", entityId)
            .WithParameter("@recordType", ListRecordTypes.Entry);

        using var iterator = _container.GetItemQueryIterator<EntryDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

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

    public async Task<int> GetNextOrderAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT VALUE MAX(c["order"]) FROM c
            WHERE c.ownerId = @ownerId
              AND c.listId = @listId
              AND c.recordType = @recordType
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", ownerId)
            .WithParameter("@listId", listId)
            .WithParameter("@recordType", ListRecordTypes.Entry);

        using var iterator = _container.GetItemQueryIterator<int?>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var max = page.Resource.FirstOrDefault();
            return (max ?? 0) + 1;
        }

        return 1;
    }

    public async Task<EntryDbo> CreateAsync(EntryDbo entry, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(entry);

        var response = await _container
            .CreateItemAsync(entry, new PartitionKey(entry.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<EntryDbo> UpdateAsync(EntryDbo entry, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(entry);

        var response = await _container
            .ReplaceItemAsync(entry, entry.Id, new PartitionKey(entry.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    private static void ValidateForWrite(EntryDbo entry)
    {
        if (StringUtilities.IsNullOrWhiteSpace(entry.Id))
        {
            throw new ArgumentException("Entry id is required.", nameof(entry));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entry.OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(entry));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entry.ListId))
        {
            throw new ArgumentException("List id is required.", nameof(entry));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entry.EntityId))
        {
            throw new ArgumentException("Entity id is required.", nameof(entry));
        }

        if (StringUtilities.IsNullOrWhiteSpace(entry.Name))
        {
            throw new ArgumentException("Entry name is required.", nameof(entry));
        }

        if (!string.Equals(entry.RecordType, ListRecordTypes.Entry, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be entry.", nameof(entry));
        }
    }
}
