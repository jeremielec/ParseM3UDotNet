using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Models;
using ParseM3UNet.Helpers;

namespace Helpers
{


    public class StrmBuilder(KnownDirectory knownDirectory, SettingsModel settingsModel) : IAsyncDisposable, IDisposable
    {



        TaskBag taskBag = new();


        public async Task<List<M3UItem>> ReadExisting(M3UItemTypeEnum m3UItemTypeEnum)
        {
            string path = m3UItemTypeEnum == M3UItemTypeEnum.MOVIE ? knownDirectory.pathMovie : knownDirectory.pathTvShow;
            return await ReadFolder(path, m3UItemTypeEnum == M3UItemTypeEnum.TVSHOW);
        }

        private async Task<List<M3UItem>> ReadFolder(string folder, bool asTvShow)
        {
            List<M3UItem> returnList = new();
            Regex m = new Regex("S(\\d\\d)");
            foreach (var groupDir in Directory.EnumerateDirectories(folder))
            {
                string groupName = Path.GetFileName(groupDir)!;
                if (asTvShow)
                {
                    foreach (var seasonDir in Directory.EnumerateDirectories(groupDir))
                    {
                        string seasonTemp = Path.GetFileName(seasonDir)!;
                        var match = m.Match(seasonTemp);
                        if (match.Success)
                        {
                            foreach (var file in Directory.EnumerateFiles(seasonDir))
                            {
                                string content = await File.ReadAllTextAsync(file);
                                string name = Path.GetFileNameWithoutExtension(file);
                                string fileName = Path.GetFileName(file);
                                returnList.Add(new(name, fileName, groupName, M3UItemTypeEnum.TVSHOW, content, seasonTemp));
                            }

                        }
                    }
                }
                else
                {
                    foreach (var movieFile in Directory.EnumerateFiles(groupDir))
                    {
                        string content = await File.ReadAllTextAsync(movieFile);
                        string name = Path.GetFileNameWithoutExtension(movieFile);
                        string fileName = Path.GetFileName(movieFile);
                        returnList.Add(new(name, fileName, groupName, M3UItemTypeEnum.MOVIE, content, null));
                    }
                }
            }

            return returnList;
        }


        public async Task Cleanup(List<M3UItem> sourceItems, M3UItemTypeEnum m3UItemTypeEnum)
        {
            //  string basePath = m3UItemTypeEnum == M3UItemTypeEnum.MOVIE ? knownDirectory.pathMovie : knownDirectory.pathTvShow;
            var itemInFs = await ReadExisting(m3UItemTypeEnum);

            foreach (var item in itemInFs)
            {
                if (sourceItems.Any(a => a.ItemType == m3UItemTypeEnum && a.GroupName == item.GroupName && a.Name == item.Name) == false)
                {
                    string filePath = item.GetStrmPath(knownDirectory);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);

                        string dir = Path.GetDirectoryName(filePath)!;

                        if (Directory.EnumerateFiles(dir).Any() == false)
                        {
                            Directory.Delete(dir);
                        }

                        if (item.ItemType == M3UItemTypeEnum.TVSHOW)
                        {
                            string showDir = Path.GetFullPath(Path.Combine(dir, ".."))!;
                            if (Directory.EnumerateDirectories(showDir).Any() == false)
                            {
                                Directory.Delete(showDir);
                            }
                        }
                    }



                }
            }
        }

        public async Task Add(M3UItem m3uItem)
        {

            // string baseDir = m3uItem.ItemType == M3UItemTypeEnum.MOVIE ? knownDirectory.pathMovie : knownDirectory.pathTvShow;
            // string subDir = Path.Combine(baseDir, m3uItem.GroupName);
            // if (m3uItem.ItemType == M3UItemTypeEnum.TVSHOW && m3uItem.Season != null)
            // {
            //     subDir = Path.Combine(subDir, m3uItem.Season);
            // }
            string extension = Path.GetExtension(m3uItem.FileName);
            string b64 = HttpUtility.UrlEncode(JsonUtils.SerializeToBase64(m3uItem.Url));
            string localProxyUrl = $"http://{settingsModel.Http.PublicIp}:{settingsModel.Http.ListenPort}/{b64}{extension}";
            string targetFile = m3uItem.GetStrmPath(knownDirectory);

            if (File.Exists(targetFile))
            {
                string content = await File.ReadAllTextAsync(targetFile);
                if (content == localProxyUrl)
                    return;
            }



            if (File.Exists(targetFile))
            {
                string currentContent = await File.ReadAllTextAsync(targetFile);
                if (currentContent.Equals(localProxyUrl)) return;
            }

            string dir = Path.GetDirectoryName(targetFile)!;
            Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(targetFile, localProxyUrl);


        }

        public void Dispose()
        {
            _ = DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await taskBag.DoAwait();
        }
    }
}