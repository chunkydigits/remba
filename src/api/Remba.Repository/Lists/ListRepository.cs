using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.Lists;
using Remba.Utility;

namespace Remba.Repository.Lists;

public class ListRepository : IListRepository
{
    private readonly Container _container;
    private readonly ILogger<ListRepository> _logger;

    public ListRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<ListRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.UserLists);
    }

    public async Task<ListDbo?> GetByIdAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId) || StringUtilities.IsNullOrWhiteSpace(listId))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<ListDbo>(listId, new PartitionKey(ownerId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == ListRecordTypes.List ? response.Resource : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<ListDbo>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(ownerId))
        {
            return [];
        }

        const string queryText = """
            SELECT * FROM c
            WHERE c.ownerId = @ownerId
              AND c.recordType = @recordType
            ORDER BY c.added DESC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerId", ownerId)
            .WithParameter("@recordType", ListRecordTypes.List);

        var results = new List<ListDbo>();
        using var iterator = _container.GetItemQueryIterator<ListDbo>(
            query,
            requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ownerId) });

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<ListDbo> CreateAsync(ListDbo list, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(list);

        var response = await _container
            .CreateItemAsync(list, new PartitionKey(list.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<ListDbo> UpdateAsync(ListDbo list, CancellationToken cancellationToken = default)
    {
        ValidateForWrite(list);

        var response = await _container
            .ReplaceItemAsync(list, list.Id, new PartitionKey(list.OwnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task DeleteAsync(string ownerId, string listId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _container
                .DeleteItemAsync<ListDbo>(listId, new PartitionKey(ownerId), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("List {ListId} for owner {OwnerId} was already missing during delete", listId, ownerId);
        }
    }

    private static void ValidateForWrite(ListDbo list)
    {
        if (StringUtilities.IsNullOrWhiteSpace(list.Id))
        {
            throw new ArgumentException("List id is required.", nameof(list));
        }

        if (StringUtilities.IsNullOrWhiteSpace(list.OwnerId))
        {
            throw new ArgumentException("Owner id is required.", nameof(list));
        }

        if (StringUtilities.IsNullOrWhiteSpace(list.Name))
        {
            throw new ArgumentException("List name is required.", nameof(list));
        }

        if (!string.Equals(list.RecordType, ListRecordTypes.List, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be list.", nameof(list));
        }
    }
}
