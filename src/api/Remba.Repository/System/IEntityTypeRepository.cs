using Remba.Model.System;

namespace Remba.Repository.System;

public interface IEntityTypeRepository
{
    Task<EntityTypeDbo?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<EntityTypeDbo?> FindByKeyAsync(
        string key,
        string? ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntityTypeDbo>> GetAvailableAsync(
        string? ownerId,
        CancellationToken cancellationToken = default);

    Task<EntityTypeDbo> CreateAsync(EntityTypeDbo entityType, CancellationToken cancellationToken = default);

    Task<EntityTypeDbo> UpdateAsync(EntityTypeDbo entityType, CancellationToken cancellationToken = default);
}
