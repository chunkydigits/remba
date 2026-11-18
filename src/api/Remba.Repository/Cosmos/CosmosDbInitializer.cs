using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;

namespace Remba.Repository.Cosmos;

/// <summary>
/// Ensures the Remba Cosmos database and core containers exist.
/// </summary>
public class CosmosDbInitializer
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbOptions _options;
    private readonly ILogger<CosmosDbInitializer> _logger;

    public CosmosDbInitializer(
        CosmosClient cosmosClient,
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbInitializer> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Ensuring Cosmos database '{DatabaseName}' exists at {Endpoint}",
            _options.DatabaseName,
            _options.Endpoint);

        var databaseResponse = await _cosmosClient
            .CreateDatabaseIfNotExistsAsync(_options.DatabaseName, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var database = databaseResponse.Database;

        await EnsureContainerAsync(database, _options.Containers.Users, "/id", cancellationToken)
            .ConfigureAwait(false);
        await EnsureContainerAsync(database, _options.Containers.UserLists, "/ownerId", cancellationToken)
            .ConfigureAwait(false);
        await EnsureContainerAsync(database, _options.Containers.Entities, "/id", cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Cosmos database and containers are ready.");
    }

    private async Task EnsureContainerAsync(
        Database database,
        string containerName,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        var response = await database
            .CreateContainerIfNotExistsAsync(
                new ContainerProperties
                {
                    Id = containerName,
                    PartitionKeyPath = partitionKeyPath
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Cosmos container '{Container}' ready (partition key {PartitionKey}, status {StatusCode})",
            containerName,
            partitionKeyPath,
            response.StatusCode);
    }
}
