using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remba.Model.Configuration;

namespace Remba.Repository.Cosmos;

/// <summary>
/// Ensures the Remba Cosmos database and core containers exist.
/// Uses shared database throughput so containers do not each reserve 400 RU/s.
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
            "Ensuring Cosmos database '{DatabaseName}' exists at {Endpoint} (shared throughput {Throughput} RU/s)",
            _options.DatabaseName,
            _options.Endpoint,
            _options.DatabaseThroughput);

        try
        {
            var databaseResponse = await CreateDatabaseAsync(cancellationToken).ConfigureAwait(false);
            var database = databaseResponse.Database;

            await EnsureContainerAsync(database, _options.Containers.Users, "/id", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.UserLists, "/ownerId", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.Entities, "/id", cancellationToken)
                .ConfigureAwait(false);
            await EnsureContainerAsync(database, _options.Containers.System, "/id", cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Cosmos database and containers are ready.");
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest && ex.SubStatusCode == 1028)
        {
            throw new InvalidOperationException(
                "Cosmos account throughput limit exceeded while creating containers. " +
                "Use shared database throughput (CosmosDb__DatabaseThroughput) and ensure existing containers " +
                "are not each provisioned at 400 RU/s. See https://aka.ms/cosmos-tp-limit",
                ex);
        }
    }

    private Task<DatabaseResponse> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        // Shared DB throughput lets all containers draw from one pool (avoids 4×400 RU/s).
        if (_options.DatabaseThroughput > 0)
        {
            return _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                throughput: _options.DatabaseThroughput,
                cancellationToken: cancellationToken);
        }

        return _cosmosClient.CreateDatabaseIfNotExistsAsync(
            _options.DatabaseName,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureContainerAsync(
        Database database,
        string containerName,
        string partitionKeyPath,
        CancellationToken cancellationToken)
    {
        // No per-container throughput: containers share the database RU/s when configured.
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
