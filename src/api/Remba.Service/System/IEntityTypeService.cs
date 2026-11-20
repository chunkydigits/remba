using Remba.Model.System;

namespace Remba.Service.System;

public interface IEntityTypeService
{
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntityTypeDto>> GetAvailableAsync(
        string? ownerId,
        CancellationToken cancellationToken = default);

    Task<EntityTypeDto> CreatePrivateAsync(
        string ownerId,
        CreateEntityTypeRequest request,
        CancellationToken cancellationToken = default);
}
