using System.ComponentModel;
using Models;
using ParseM3UNet.Helpers;

namespace Helpers
{


    public class StrmBuilder(KnownDirectory knownDirectory, SettingsModel settingsModel): IAsyncDisposable,IDisposable
    {



        TaskBag taskBag = new();

        public async Task Add(M3UItem m3uItem)
        {

            string baseDir = m3uItem.ItemType == M3UItemTypeEnum.MOVIE ? knownDirectory.pathMovie : knownDirectory.pathTvShow;
            string subDir = Path.Combine(baseDir, m3uItem.GroupName);
            string targetFile = Path.Combine(subDir, m3uItem.Name.Replace('/', ' ') + ".strm");
            Directory.CreateDirectory(subDir);
            string b64 = JsonUtils.SerializeToBase64(m3uItem.Url);
            string localProxyUrl = $"http://{settingsModel.Http.PublicIp}:{settingsModel.Http.ListenPort}/{b64}";
            taskBag.Add(File.WriteAllTextAsync(targetFile, localProxyUrl), "writing " + m3uItem.FileName);
            if (taskBag.ShouldAwait())
                await taskBag.DoAwait();

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