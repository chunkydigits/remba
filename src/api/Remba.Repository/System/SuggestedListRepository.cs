using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;
using Remba.Model.System;
using Remba.Utility;

namespace Remba.Repository.System;

public class SuggestedListRepository : ISuggestedListRepository
{
    private readonly Container _container;
    private readonly ILogger<SuggestedListRepository> _logger;

    public SuggestedListRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<SuggestedListRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(cosmosClient);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var settings = options.Value;
        _container = cosmosClient.GetContainer(settings.DatabaseName, settings.Containers.System);
    }

    public async Task<SuggestedListDbo?> GetBySeedKeyAsync(string seedKey, CancellationToken cancellationToken = default)
    {
        if (StringUtilities.IsNullOrWhiteSpace(seedKey))
        {
            return null;
        }

        try
        {
            var response = await _container
                .ReadItemAsync<SuggestedListDbo>(seedKey, new PartitionKey(seedKey), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return response.Resource.RecordType == SystemRecordTypes.SuggestedList
                ? response.Resource
                : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SuggestedListDbo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string queryText = """
            SELECT * FROM c
            WHERE c.recordType = @recordType
            ORDER BY c.sortOrder ASC
            """;

        var query = new QueryDefinition(queryText)
            .WithParameter("@recordType", SystemRecordTypes.SuggestedList);

        var results = new List<SuggestedListDbo>();
        using var iterator = _container.GetItemQueryIterator<SuggestedListDbo>(query);

        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            results.AddRange(page.Resource);
        }

        return results;
    }

    public async Task<SuggestedListDbo> CreateAsync(
        SuggestedListDbo suggestedList,
        CancellationToken cancellationToken = default)
    {
        ValidateForWrite(suggestedList);

        var response = await _container
            .CreateItemAsync(
                suggestedList,
                new PartitionKey(suggestedList.Id),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    public async Task<SuggestedListDbo> UpdateAsync(
        SuggestedListDbo suggestedList,
        CancellationToken cancellationToken = default)
    {
        ValidateForWrite(suggestedList);

        var response = await _container
            .ReplaceItemAsync(
                suggestedList,
                suggestedList.Id,
                new PartitionKey(suggestedList.Id),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.Resource;
    }

    private static void ValidateForWrite(SuggestedListDbo suggestedList)
    {
        if (StringUtilities.IsNullOrWhiteSpace(suggestedList.Id))
        {
            throw new ArgumentException("Suggested list id is required.", nameof(suggestedList));
        }

        if (StringUtilities.IsNullOrWhiteSpace(suggestedList.SeedKey))
        {
            throw new ArgumentException("Seed key is required.", nameof(suggestedList));
        }

        if (!string.Equals(suggestedList.Id, suggestedList.SeedKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Suggested list id must match seed key.", nameof(suggestedList));
        }

        if (StringUtilities.IsNullOrWhiteSpace(suggestedList.Name))
        {
            throw new ArgumentException("Suggested list name is required.", nameof(suggestedList));
        }

        if (!string.Equals(suggestedList.RecordType, SystemRecordTypes.SuggestedList, StringComparison.Ordinal))
        {
            throw new ArgumentException("Record type must be suggestedList.", nameof(suggestedList));
        }
    }
}
