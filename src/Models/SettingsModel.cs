
namespace Models
{
  // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Http
    {
        public string PublicIp { get; set; } = default!;
        public int ListenPort { get; set; }
        public string UserAgent { get; set; } = default!;
    }

    public class Output
    {
        public string Folder { get; set; } = default!;
        public int MaxCacheItem { get; set; }
    }

    public class RegexModel
    {
        public string HeaderMatch { get; set; }= default!;
        public List<string> SkipRegex { get; set; }= default!;
        public List<string> GroupGenRegex { get; set; }= default!;
        public TvShow TvShow { get; set; }= default!;
    }

    public class SettingsModel
    {
        public Source Source { get; set; }= default!;
        public RegexModel Regex { get; set; }= default!;
        public Output Output { get; set; }= default!;
        public Http Http { get; set; }= default!;
    }

    public class Source
    {
        public string? LocalFileM3U { get; set; }= default!;
        public string? HttpM3USource { get; set; }= default!;
        public double SyncIntervalHour { get; set; }
    }

    public class TvShow
    {
        public List<string> SeasonRegex { get; set; }= default!;
        public List<string> EpisodeRegex { get; set; }= default!;
    }




}
