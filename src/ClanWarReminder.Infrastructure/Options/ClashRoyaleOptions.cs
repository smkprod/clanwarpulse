namespace ClanWarReminder.Infrastructure.Options;

public class ClashRoyaleOptions
{
    public const string SectionName = "ClashRoyale";
    public string BaseUrl { get; set; } = "https://api.clashroyale.com/v1";
    public string ApiToken { get; set; } = string.Empty;
}
