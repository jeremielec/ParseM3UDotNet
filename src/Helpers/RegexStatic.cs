

using System.Text.RegularExpressions;
using Models;

public class RegexStatic
{

    public static RegexStatic Instance = default!;


    public RegexStatic(SettingsModel settingsModel)
    {
        this.M3UHeaderRegEx = Create(settingsModel.Regex.HeaderMatch);

        this.M3USeasonRegEx = Create(settingsModel.Regex.TvShow.SeasonRegex);
        this.M3UEpisodeRegEx = Create(settingsModel.Regex.TvShow.EpisodeRegex);

        this.M3USkipRegEx = Create(settingsModel.Regex.SkipRegex);
        this.M3UGroupGenRegEx = Create(settingsModel.Regex.GroupGenRegex);

        this.HttpUrlRegex = Create(@"^/([\w\d=]*)$");

    }

    public readonly Regex M3UHeaderRegEx ;
    public readonly List<Regex> M3USeasonRegEx;
    public readonly List<Regex> M3UEpisodeRegEx;
    public readonly List<Regex> M3USkipRegEx;
    public readonly List<Regex> M3UGroupGenRegEx;

    public Regex HttpUrlRegex { get; }

    private Regex Create(string reg) => new Regex(reg, RegexOptions.Compiled);
    private List<Regex> Create(IEnumerable<string> reg) => reg.Select(a => new Regex(a, RegexOptions.Compiled)).ToList();
}