using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Application.Services;

public class ClanSetupService
{
    private readonly IGroupRepository _groups;
    private readonly IUnitOfWork _unitOfWork;

    public ClanSetupService(IGroupRepository groups, IUnitOfWork unitOfWork)
    {
        _groups = groups;
        _unitOfWork = unitOfWork;
    }

    public async Task<Group> SetupAsync(PlatformType platform, string platformGroupId, string clanTag, CancellationToken cancellationToken)
    {
        var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(clanTag);
        var existing = await _groups.GetByPlatformGroupAsync(platform, platformGroupId, cancellationToken);

        if (existing is not null)
        {
            existing.ClanTag = normalizedTag;
            existing.IsActive = true;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var group = new Group
        {
            Platform = platform,
            PlatformGroupId = platformGroupId,
            ClanTag = normalizedTag,
            IsActive = true
        };

        await _groups.AddAsync(group, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return group;
    }
}
