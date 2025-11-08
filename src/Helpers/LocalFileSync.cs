using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Numerics;
using Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Models;
using ParseM3UNet.Helpers;

public class LocalFileSync
{
    private FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
    private readonly SettingsModel settingsModel;
    private readonly ILogger<LocalFileSync> logger;
    private readonly FileDownloader fileDownloader;
    private readonly KnownDirectory knownDirectory;

    public LocalFileSync(KnownDirectory knownDirectory, SettingsModel settingsModel, ILogger<LocalFileSync> logger, FileDownloader fileDownloader)
    {
        this.knownDirectory = knownDirectory;
        this.settingsModel = settingsModel;
        this.logger = logger;
        this.fileDownloader = fileDownloader;
        provider.Mappings.Add(".mkv", "video/x-matroska");
    }




    public async Task SyncAndServerFile(HttpContext httpContext, string targetUrl)
    {

        if (!provider.TryGetContentType(targetUrl, out string? mime))
        {
            mime = "application/octet-stream"; // par défaut si inconnu
        }
        httpContext.Response.ContentType = mime;

        string cacheFile = Path.Combine(knownDirectory.pathCacheDir, GetCacheFileName(targetUrl));
        string tmpFile = cacheFile + ".tmp";



        if (File.Exists(cacheFile))
        {
            logger.LogInformation($"Serving url from local cache {targetUrl}");
            UpdateLastAccessTime(cacheFile);
            await ServeFromLocalFile(httpContext, cacheFile);
        }
        else
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            //  Task? downloadTaskJob = null;
            if (File.Exists(tmpFile) == false)
            {
                logger.LogInformation($"Downloading and serving url  {targetUrl}");
                fileDownloader.Add(targetUrl, tmpFile, (a, b) => onDownloadCompleted(a, b, cacheFile));

                Stopwatch pendingFileExistsWatch = Stopwatch.StartNew();
                while (pendingFileExistsWatch.Elapsed.TotalSeconds < 15)
                {
                    if (File.Exists(tmpFile)) break;
                }
                if (File.Exists(tmpFile) == false)
                {
                    logger.LogWarning($"{tmpFile} doest not exists after 15 second, something is wrong");
                    httpContext.Response.StatusCode = 500;
                    await httpContext.Response.StartAsync();
                    return;
                }

                // downloadTaskJob = DownloadFile(targetUrl, tmpFile);
            }
            else
            {
                logger.LogInformation($"Serving url from tmpfile  {targetUrl}");
            }

            long progress = 0;


            while (File.Exists(tmpFile))
            {
                if (httpContext.RequestAborted.IsCancellationRequested == false)
                {
                    progress = await ServeFromLocalFile(httpContext, tmpFile, progress);
                }

                await Task.Delay(1000);
            }

            if (File.Exists(cacheFile))
            {
                if (httpContext.RequestAborted.IsCancellationRequested == false)
                    await ServeFromLocalFile(httpContext, cacheFile, progress);
            }




        }

        ClearCacheFolder();

    }

    private void onDownloadCompleted(FileDownloaderItem item, DownloadStatusEnum @enum, string targetCacheFile)
    {
        if (@enum == DownloadStatusEnum.FAILED)
        {
            if (File.Exists(item.TargetFile))
                File.Delete(item.TargetFile);
        }
        else if (@enum == DownloadStatusEnum.COMPLETED)
        {
            if (File.Exists(item.TargetFile))
                File.Move(item.TargetFile, targetCacheFile);
        }
    }


    private string GetCacheFileName(string url) => JsonUtils.SerializeToBase64(url) + '.' + url.Split('.').Last();

    private void UpdateLastAccessTime(string cacheFile)
    {
        DateTime nouvelleDate = DateTime.Now;
        File.SetLastAccessTime(cacheFile, nouvelleDate);
    }

    public async Task<long> ServeFromLocalFile(HttpContext httpContext, string localFile, long? seekOffset = null)
    {
        using (Stream stream = File.Open(localFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (seekOffset != null)
                stream.Seek(seekOffset.Value, SeekOrigin.Begin);

            await stream.CopyToAsync(httpContext.Response.Body);
            return stream.Position;

        }
    }

    // private async Task DownloadFile(string url, string target)
    // {
    //     using (FileStream writeStream = File.Open(target, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
    //     {
    //         using (HttpClient client = new HttpClient())
    //         {
    //             var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
    //             response.EnsureSuccessStatusCode();
    //             await response.Content.CopyToAsync(writeStream);
    //         }
    //     }
    // }


    private void ClearCacheFolder()
    {
        var files = Directory.EnumerateFiles(knownDirectory.pathCacheDir);
        files = files.Where(a => a.EndsWith(".tmp") == false).ToList();

        if (files.Count() < this.settingsModel.Output.MaxCacheItem) return;

        var lastAcceded = files.Select(a => new { File = a, LastAccess = File.GetLastAccessTime(a) });

        var tries = lastAcceded
            .OrderByDescending(kv => kv.LastAccess)
            .ToList();

        var fileToDeletes = tries
            .Skip(Math.Max(0, tries.Count - this.settingsModel.Output.MaxCacheItem))
            .ToList();

        foreach (var toDelete in fileToDeletes)
        {
            logger.LogInformation($"deleting {toDelete}");
            File.Delete(toDelete.File);
        }

    }




}