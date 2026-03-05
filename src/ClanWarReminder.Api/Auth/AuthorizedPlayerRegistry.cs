using System.Collections.Concurrent;

namespace ClanWarReminder.Api.Auth;

public sealed class AuthorizedPlayerRegistry
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _authorizedTags = new(StringComparer.OrdinalIgnoreCase);

    public void MarkAuthorized(string playerTag)
    {
        var normalized = NormalizeTag(playerTag);
        _authorizedTags[normalized] = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<string> GetAuthorizedTagsForMembers(IEnumerable<string> memberTags)
    {
        var result = new List<string>();

        foreach (var tag in memberTags)
        {
            var normalized = NormalizeTag(tag);
            if (_authorizedTags.ContainsKey(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static string NormalizeTag(string tag)
    {
        var value = tag.Trim().ToUpperInvariant();
        return value.StartsWith('#') ? value : $"#{value}";
    }
}
