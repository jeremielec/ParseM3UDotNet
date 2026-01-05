using System.ComponentModel;
using System.Net;
using System.Text.Json;
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
            return await ReadFolder(path);
        }

        private async Task<List<M3UItem>> ReadFolder(string folder)
        {
            List<M3UItem> returnList = new();

            foreach (string[] files in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories).Chunk(100))
            {

                List<M3UItem> subList = new();
                foreach (var f in files)
                {
                    string content = await File.ReadAllTextAsync(f);
                    M3UItem m3UItem = JsonSerializer.Deserialize<M3UItem>(content)!;
                    subList.Add(m3UItem);
                }

                lock (returnList)
                {
                    returnList.AddRange(subList);
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

                        string json = Path.ChangeExtension(filePath, "json");
                        if (File.Exists(json))
                            File.Delete(json);


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

            string extension = Path.GetExtension(m3uItem.FileName);
            string b64 = HttpUtility.UrlEncode(JsonUtils.SerializeToBase64(m3uItem.Url));
            string localProxyUrl = $"http://{settingsModel.Http.PublicIp}:{settingsModel.Http.ListenPort}/{b64}{extension}";
            string targetFile = m3uItem.GetStrmPath(knownDirectory);
            string targetFileJson = Path.ChangeExtension(targetFile, "json");

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
            await File.WriteAllTextAsync(targetFileJson, JsonSerializer.Serialize(m3uItem));


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