using Remba.Model.System;

namespace Remba.Repository.System;

public interface ISuggestedListRepository
{
    Task<SuggestedListDbo?> GetBySeedKeyAsync(string seedKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SuggestedListDbo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SuggestedListDbo> CreateAsync(SuggestedListDbo suggestedList, CancellationToken cancellationToken = default);

    Task<SuggestedListDbo> UpdateAsync(SuggestedListDbo suggestedList, CancellationToken cancellationToken = default);
}
