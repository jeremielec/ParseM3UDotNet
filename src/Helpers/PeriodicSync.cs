using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;

namespace ParseM3UNet.Helpers
{
    public class PeriodicSync(IServiceProvider serviceProvider, KnownDirectory knownDirectory, SettingsModel settingsModel, ILogger<PeriodicSync> logger) : IHostedService
    {
        CancellationTokenSource cancellationTokenSource = new();
        private readonly IServiceProvider serviceProvider = serviceProvider;
        private readonly KnownDirectory knownDirectory = knownDirectory;
        private readonly SettingsModel settingsModel = settingsModel;
        private readonly ILogger<PeriodicSync> logger = logger;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = InternalRun();
            return Task.CompletedTask;
        }

        private async Task InternalRun()
        {

            while (cancellationTokenSource.IsCancellationRequested == false)
            {

                try
                {
                    await Sync();
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Periodic sync error");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromHours(settingsModel.Source.SyncIntervalHour));
                }
                catch (TaskCanceledException) { }
            }

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        private async Task Sync()
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var knownDirectory = scope.ServiceProvider.GetRequiredService<KnownDirectory>();
                var m3UParser = scope.ServiceProvider.GetRequiredService<M3UParser>();
                var strmBuilder = scope.ServiceProvider.GetRequiredService<StrmBuilder>();

                knownDirectory.Clear();
                string source;
                if (settingsModel.Source.LocalFileM3U != null && settingsModel.Source.HttpM3USource != null)
                {
                    throw new Exception("LocalFileM3U and HttpM3USource cannot be set both");
                }
                else if (settingsModel.Source.LocalFileM3U != null)
                {
                    source = settingsModel.Source.LocalFileM3U;
                }
                else if (settingsModel.Source.HttpM3USource != null)
                {
                    source = await DownloadFromHttp(settingsModel.Source.HttpM3USource);
                }
                else
                {
                    throw new Exception("No source M3U");
                }

                int movieParsed = 0, tvShowParsed = 0;
                using (FileStream stream = File.OpenRead(source))
                {
                    await foreach (var item in m3UParser.ReadM3U(stream))
                    {
                        if (item.ItemType == M3UItemTypeEnum.MOVIE) movieParsed++;
                        if (item.ItemType == M3UItemTypeEnum.TVSHOW) tvShowParsed++;

                        await strmBuilder.Add(item);
                    }
                }
                logger.LogInformation($"Parse M3U completed, movie = {movieParsed} tvshow = {tvShowParsed}");
            }

        }

        private async Task<string> DownloadFromHttp(string httpM3USource)
        {
            using (var client = new HttpClient())
            {
                var get = await client.GetAsync(httpM3USource, HttpCompletionOption.ResponseHeadersRead);
                get.EnsureSuccessStatusCode();
                string name = "playlist.m3u";
                string tempFile = Path.Combine(settingsModel.Output.Folder, name);

                using (FileStream stream = File.Open(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var responseStream = await get.Content.ReadAsStreamAsync();
                    await responseStream.CopyToAsync(stream);
                }
                return tempFile;
            }

        }
    }

}


