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
    private readonly KnownDirectory knownDirectory;

    public LocalFileSync(KnownDirectory knownDirectory, SettingsModel settingsModel, ILogger<LocalFileSync> logger)
    {
        this.knownDirectory = knownDirectory;
        this.settingsModel = settingsModel;
        this.logger = logger;
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
            if (HasSyncInProgress(tmpFile))
            {
                logger.LogInformation("A request is already in progress, pending completion");
                do
                {
                    await Task.Delay(1000);
                } while (HasSyncInProgress(tmpFile));
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            Task? downloadTaskJob = null;
            if (File.Exists(tmpFile) == false)
            {
                logger.LogInformation($"Downloading and serving url  {targetUrl}");
                downloadTaskJob = DownloadFile(targetUrl, tmpFile);
            }
            else
            {
                logger.LogInformation($"Serving url from tmpfile  {targetUrl}");
            }

            long progress = 0;


            while (File.Exists(tmpFile))
            {
                await Task.Delay(1000);
                if (httpContext.RequestAborted.IsCancellationRequested == false)
                {
                    progress = await ServeFromLocalFile(httpContext, tmpFile, progress);
                }

                if (downloadTaskJob != null && downloadTaskJob.IsCompleted)
                    break;

            }
            if (downloadTaskJob != null)
            {
                try
                {
                    await downloadTaskJob;
                }
                catch (Exception e)
                {
                    File.Delete(tmpFile);
                    logger.LogError(e, "download failed");
                    return;
                }
                File.Move(tmpFile, cacheFile);
                logger.LogInformation($"Downloading url {targetUrl} completed after {stopwatch.Elapsed}");
            }


            await ServeFromLocalFile(httpContext, cacheFile, progress);
        }

        ClearCacheFolder();

    }

    private bool HasSyncInProgress(string selfFile) => Directory
        .EnumerateFiles(this.knownDirectory.pathCacheDir)
        .Where(a => a != selfFile)
        .Any(a => a.EndsWith(".tmp"));
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

    private async Task DownloadFile(string url, string target)
    {
        using (FileStream writeStream = File.Open(target, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                await response.Content.CopyToAsync(writeStream);
            }
        }
    }


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
            File.Delete(toDelete.File);
        }

    }




}