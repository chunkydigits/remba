using Remba.Model.Entities;

namespace Remba.Repository.Entities;

public interface IEntityRepository
{
    Task<EntityDbo?> GetByIdAsync(string entityId, CancellationToken cancellationToken = default);

    Task<EntityDbo?> FindByIdentifierAsync(
        string identifierType,
        string identifierValue,
        CancellationToken cancellationToken = default);

    Task<EntityDbo> CreateAsync(EntityDbo entity, CancellationToken cancellationToken = default);

    Task<EntityDbo> UpdateAsync(EntityDbo entity, CancellationToken cancellationToken = default);
}
