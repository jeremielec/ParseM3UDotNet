

using System.Text.RegularExpressions;

namespace Helpers
{


    public class M3UParser()
    {

        public async IAsyncEnumerable<M3UItem> ReadM3U(Stream stream)
        {

            using (StreamReader reader = new StreamReader(stream))
            {
                string? line = await reader.ReadLineAsync();
                Match? previousHeaderMatch = null;

                do
                {
                    if (line != null)
                    {
                        if (previousHeaderMatch != null)
                        {
                            string url = line;
                            string rawName = previousHeaderMatch.Groups.Values.Last().Value;
                            string? seasonInfo = RegexStatic.Instance.M3USeasonRegEx
                                .Select(a => a.Match(rawName))
                                .Where(a => a.Success)
                                .Select(a => a.Captures.Last().Value)
                                .FirstOrDefault();
                            bool hasEpisodeInfo = RegexStatic.Instance.M3UEpisodeRegEx.Any(a => a.Match(rawName).Success);
                            M3UItemTypeEnum m3UItemTypeEnum = hasEpisodeInfo && seasonInfo != null ? M3UItemTypeEnum.TVSHOW : M3UItemTypeEnum.MOVIE;

                            M3UItem m3UItem = new M3UItem(
                                Name: rawName,
                                FileName: url.Split('/').Last(),
                                GroupName: GetGroupName(rawName),
                                ItemType: m3UItemTypeEnum,
                                Season: seasonInfo,
                                Url: url
                            );
                            yield return m3UItem;

                            previousHeaderMatch = null;
                        }
                        else
                        {
                            var matchHeader = RegexStatic.Instance.M3UHeaderRegEx.Match(line);
                            if (matchHeader.Success)
                            {
                                previousHeaderMatch = matchHeader;
                            }
                        }


                        line = await reader.ReadLineAsync();
                    }
                } while (line != null);

            }
        }

        private string GetGroupName(string rawName)
        {
            string tempName = rawName;

            var alLReg = RegexStatic.Instance.M3UGroupGenRegEx
                .Concat(RegexStatic.Instance.M3USeasonRegEx)
                .Concat(RegexStatic.Instance.M3UEpisodeRegEx);

            foreach (var reg in alLReg)
            {
                tempName = reg.Replace(tempName, "");
            }

            while (tempName.Contains("  "))
                tempName = tempName.Replace("  ", " ");
            tempName = tempName.Replace('/', ' ');
            tempName = tempName.Replace('\\', ' ');

            return tempName;
        }
    }
}