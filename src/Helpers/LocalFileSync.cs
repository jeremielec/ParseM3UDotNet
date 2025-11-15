using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Numerics;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Models;
using ParseM3UNet.Helpers;
using ParseM3UNet.Http;
using ParseM3UNet.StreamUtils;

public class LocalFileSync
{
    private FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider();
    private readonly SettingsModel settingsModel;
    private readonly ILogger<LocalFileSync> logger;
    private readonly FileDownloader fileDownloader;
    private readonly RegexRepository regexRepository;
    private readonly KnownDirectory knownDirectory;

    public LocalFileSync(
        KnownDirectory knownDirectory, SettingsModel settingsModel,
        ILogger<LocalFileSync> logger, FileDownloader fileDownloader,
        RegexRepository regexRepository)
    {
        this.knownDirectory = knownDirectory;
        this.settingsModel = settingsModel;
        this.logger = logger;
        this.fileDownloader = fileDownloader;
        this.regexRepository = regexRepository;
        provider.Mappings.Add(".mkv", "video/x-matroska");
    }




    public async Task SyncAndServerFile(HttpContext httpContext, string targetUrl)
    {
        long? startOffset = null;
        long? endOffset = null;

        string cacheFile = Path.Combine(knownDirectory.pathCacheDir, GetCacheFileName(targetUrl));
        PartialFileStream partialFileStream = new PartialFileStream(cacheFile);


        httpContext.Response.Headers.AcceptRanges = "bytes";
        if (httpContext.Request.Headers.Range.Any())
        {
            startOffset = 0;
            if (httpContext.Request.Headers.Range.Count() > 1)
            {
                httpContext.Response.StatusCode = 416;
                return;
            }


            if (RangeHeaderValue.TryParse(httpContext.Request.Headers.Range, out var range))
            {
                var rangeCurrent = range.Ranges.FirstOrDefault();
                if (rangeCurrent != null)
                {
                    if (rangeCurrent.From != null)
                        startOffset = rangeCurrent.From.Value;
                    if (rangeCurrent.To != null)
                        endOffset = rangeCurrent.To.Value;
                }
            }
        }




        if (startOffset != null)
        {
            partialFileStream.CurrentOffset = startOffset.Value;
        }

        if (File.Exists(cacheFile) == false)
        {
            if (File.Exists(partialFileStream.tempFileName) == false)
            {
                if (fileDownloader.UrlInProgress(targetUrl) == false)
                    fileDownloader.Add(targetUrl, partialFileStream.tempFileName, (a, b) => onDownloadCompleted(a, b, cacheFile));
                WaitForFileExists(partialFileStream.tempFileName);
            }
        }

        /* Set response headers */
        long? contentLength = GetFileSize(targetUrl, partialFileStream);

        if (startOffset != null)
        {
            httpContext.Response.StatusCode = 206;
            var length = (endOffset ?? contentLength);
            httpContext.Response.Headers.ContentRange = $"bytes {startOffset}-{length}/{contentLength}";
            httpContext.Response.ContentLength = length;

        }
        else
        {
            httpContext.Response.StatusCode = 200;
            httpContext.Response.ContentLength = contentLength;
        }


        if (!provider.TryGetContentType(targetUrl, out string? mime))
        {
            mime = "application/octet-stream"; // par défaut si inconnu
        }
        httpContext.Response.ContentType = mime;
        httpContext.Response.Headers.AcceptRanges = "bytes";


        this.logger.LogInformation($"Serving file range start : {startOffset} end: {endOffset} length : {contentLength}");
        this.logger.LogInformation($"Content Range response : {httpContext.Response.Headers.ContentRange.FirstOrDefault()}");

        if (httpContext.Request.Method.Equals("head", StringComparison.InvariantCultureIgnoreCase) == false)
            await ServeFromLocalFile(httpContext, partialFileStream, endOffset);

    }

    private long? GetFileSize(string url, PartialFileStream partialFileStream)
    {
        if (File.Exists(partialFileStream.fileName))
        {
            var info = new FileInfo(partialFileStream.fileName);
            return info.Length;
        }
        if (File.Exists(partialFileStream.tempFileName))
        {
            return fileDownloader.GetContentLengthForUrl(url);
        }
        return null;
    }

    private void WaitForFileExists(string file)
    {

        Stopwatch pendingFileExistsWatch = Stopwatch.StartNew();
        while (pendingFileExistsWatch.Elapsed.TotalSeconds < 15)
        {
            if (File.Exists(file)) break;
        }
        if (File.Exists(file) == false)
        {
            throw new TimeoutException($"{file} doest not exists after 15 second, something is wrong");
        }
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



    public async Task ServeFromLocalFile(HttpContext httpContext, PartialFileStream partialFileStream, long? endOffset)
    {
        int? readed;
        bool interruptRequested =false;
        do
        {
            readed = await partialFileStream.Read();
            if (readed != null)
            {
                if (partialFileStream.CurrentOffset > endOffset && endOffset != null)
                {
                    interruptRequested = true;
                    readed = (int)(partialFileStream.CurrentOffset - endOffset.Value);
                }

                await httpContext.Response.Body.WriteAsync(partialFileStream.Data, 0, readed.Value);

                if (readed == 0)
                    await Task.Delay(2000);

                if (httpContext.RequestAborted.IsCancellationRequested)
                    break;
            }
        } while (readed != null && interruptRequested == false);

        // using (Stream stream = File.Open(localFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        // {
        //     if (seekOffset != null)
        //         stream.Seek(seekOffset.Value, SeekOrigin.Begin);

        //     await stream.CopyToAsync(httpContext.Response.Body);
        //    return stream.Position;
        // }
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
            logger.LogInformation($"deleting {toDelete}");
            File.Delete(toDelete.File);
        }

    }




}