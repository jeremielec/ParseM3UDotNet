

using System.Text.RegularExpressions;
using ParseM3UNet.Helpers;

namespace ParseM3UNet.M3U
{


    public class M3UParser(RegexRepository regexRepository)
    {
        private readonly RegexRepository regexRepository = regexRepository;

        public async Task<(List<M3UItem> Movies, List<M3UItem> TvShows)> GetM3UItems(Stream stream)
        {
            List<M3UItem> Movies = new();
            List<M3UItem> TvShows = new();

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
                            string? seasonInfo = regexRepository.M3USeasonRegEx
                                .Select(a => a.Match(rawName))
                                .Where(a => a.Success)
                                .Select(a => a.Captures.Last().Value)
                                .FirstOrDefault();
                            bool hasEpisodeInfo = regexRepository.M3UEpisodeRegEx.Any(a => a.Match(rawName).Success);
                            M3UItemTypeEnum m3UItemTypeEnum = hasEpisodeInfo && seasonInfo != null ? M3UItemTypeEnum.TVSHOW : M3UItemTypeEnum.MOVIE;

                            M3UItem m3UItem = new M3UItem(
                                Name: rawName,
                                FileName: url.Split('/').Last(),
                                GroupName: GetGroupName(rawName),
                                ItemType: m3UItemTypeEnum,
                                Season: seasonInfo,
                                Url: url
                            );

                            if (m3UItem.ItemType == M3UItemTypeEnum.MOVIE)
                                Movies.Add(m3UItem);
                            else
                                TvShows.Add(m3UItem);

                            // yield return m3UItem;

                            previousHeaderMatch = null;
                        }
                        else
                        {
                            var matchHeader = regexRepository.M3UHeaderRegEx.Match(line);
                            if (matchHeader.Success)
                            {
                                bool shouldSkip = regexRepository.M3USkipRegEx.Any(a => a.Match(line).Success);
                                if (shouldSkip == false)
                                    previousHeaderMatch = matchHeader;
                            }
                        }


                        line = await reader.ReadLineAsync();
                    }
                } while (line != null);

            }

            return (Movies, TvShows);
        }

        private string GetGroupName(string rawName)
        {
            string tempName = rawName;

            var alLReg = regexRepository.M3UGroupGenRegEx
                .Concat(regexRepository.M3USeasonRegEx)
                .Concat(regexRepository.M3UEpisodeRegEx);

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