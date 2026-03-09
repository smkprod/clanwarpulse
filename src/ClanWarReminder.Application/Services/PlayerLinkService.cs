using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Application.Services;

public class PlayerLinkService
{
    private readonly IGroupRepository _groups;
    private readonly IUserRepository _users;
    private readonly IPlayerLinkRepository _links;
    private readonly IUnitOfWork _unitOfWork;

    public PlayerLinkService(
        IGroupRepository groups,
        IUserRepository users,
        IPlayerLinkRepository links,
        IUnitOfWork unitOfWork)
    {
        _groups = groups;
        _users = users;
        _links = links;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlayerLink> LinkAsync(
        PlatformType platform,
        string platformGroupId,
        string platformUserId,
        string displayName,
        string playerTag,
        CancellationToken cancellationToken)
    {
        var group = await _groups.GetByPlatformGroupAsync(platform, platformGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Group is not configured. Run /setup first.");

        var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(playerTag);

        var user = await _users.GetByPlatformUserAsync(platform, platformUserId, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Platform = platform,
                PlatformUserId = platformUserId,
                DisplayName = displayName
            };
            await _users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.DisplayName = displayName;
        }

        var existing = await _links.GetByGroupAndUserAsync(group.Id, user.Id, cancellationToken);
        if (existing is not null)
        {
            existing.PlayerTag = normalizedTag;
            existing.LinkedAtUtc = DateTimeOffset.UtcNow;
            await _links.UpdateAsync(existing, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var link = new PlayerLink
        {
            GroupId = group.Id,
            UserId = user.Id,
            PlayerTag = normalizedTag
        };

        await _links.AddAsync(link, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return link;
    }
}
