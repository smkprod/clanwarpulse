namespace ClanWarReminder.Domain.Common;

public static class TagNormalizer
{
    public static string NormalizeClanOrPlayerTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new ArgumentException("Tag is required.", nameof(tag));
        }

        var cleaned = tag.Trim().ToUpperInvariant();
        if (!cleaned.StartsWith('#'))
        {
            cleaned = $"#{cleaned}";
        }

        return cleaned;
    }
}
