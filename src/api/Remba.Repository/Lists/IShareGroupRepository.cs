using Remba.Model.Lists;

namespace Remba.Repository.Lists;

public interface IShareGroupRepository
{
    Task<ShareGroupDbo?> GetByIdAsync(string ownerId, string groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareGroupDbo>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<ShareGroupDbo> CreateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default);

    Task<ShareGroupDbo> UpdateAsync(ShareGroupDbo group, CancellationToken cancellationToken = default);

    Task DeleteAsync(string ownerId, string groupId, CancellationToken cancellationToken = default);
}
