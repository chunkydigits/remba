using Remba.Model.Lists;

namespace Remba.Service.Lists;

public interface IShareGroupService
{
    Task<IReadOnlyList<ShareGroupDto>> GetOwnAsync(string ownerId, CancellationToken cancellationToken = default);

    Task<ShareGroupDto> CreateAsync(
        string ownerId,
        CreateShareGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<ShareGroupDto> UpdateAsync(
        string ownerId,
        string groupId,
        UpdateShareGroupRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string ownerId, string groupId, CancellationToken cancellationToken = default);

    Task<ShareGroupDbo?> GetOwnedGroupAsync(
        string ownerId,
        string groupId,
        CancellationToken cancellationToken = default);
}
