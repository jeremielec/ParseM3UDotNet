using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ParseM3UNet.Helpers;

public class FileDownloader
{
    private readonly ILogger<FileDownloader> logger;
    private List<FileDownloaderItem> items = new();
    byte[] dataArray = new byte[65535];
    HttpClient httpClient = new();


    public FileDownloader(ILogger<FileDownloader> logger)
    {
        this.logger = logger;
        _ = BackgroundJob();
    }

    public const int CONST_TRYCOUNT = 3;

    public void Add(string TargetUrl, string TargetFile, Action<FileDownloaderItem, DownloadStatusEnum> callback)
    {
        lock (items)
        {
            items.ForEach(a => a.AbortRequested = true);
            items.Add(new(TargetUrl, TargetFile, callback));
        }
    }

    private async Task BackgroundJob()
    {

        while (true)
        {

            await Task.Delay(1000);
            lock (items)
                if (items.Count == 0)
                    continue;

            var toHandle = items
                .Where(a => a.nextTry == null || a.nextTry < DateTime.Now)
                .OrderBy(a => a.LastRunTime).First();
            toHandle.AbortRequested = false;
            toHandle.LastRunTime = DateTime.Now;

            Stopwatch runWatch = Stopwatch.StartNew();
            var downloadTask = DownloadFile(toHandle);

            bool cond = true;

            while (cond)
            {
                await Task.Delay(1000);

                lock (items)
                    cond = items.Count < 2 || runWatch.Elapsed.TotalSeconds < 30;
                if (downloadTask.IsCompleted) break;
                if (cond == false && downloadTask.IsCompleted == false) toHandle.AbortRequested = true;
            }

            var finalStatus = await downloadTask;
            if (finalStatus == DownloadStatusEnum.COMPLETED)
            {
                logger.LogInformation("Download completed " + toHandle.TargetUrl);
                lock (items) items.Remove(toHandle);
            }
            else
            {
                logger.LogDebug($"Download suspended " + toHandle.TargetUrl);
            }

            if (finalStatus != DownloadStatusEnum.SUSPENDED)
            {
                try
                {
                    toHandle.DownloadCallBack(toHandle, finalStatus);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "callback error");
                }
                lock (items) items.Remove(toHandle);
            }

        }

    }


    private async Task<DownloadStatusEnum> DownloadFile(FileDownloaderItem fileDownloaderItem)
    {
        FileStream outputStream;
        if (fileDownloaderItem.PositionOffset == 0)
        {
            outputStream = File.Open(fileDownloaderItem.TargetFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        }
        else
        {
            outputStream = File.Open(fileDownloaderItem.TargetFile, FileMode.Open, FileAccess.Write, FileShare.Read);
            outputStream.Seek(fileDownloaderItem.PositionOffset, SeekOrigin.Begin);
        }

        using (outputStream)
        {

            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, fileDownloaderItem.TargetUrl);
            httpRequestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(fileDownloaderItem.PositionOffset, null);
            try
            {
                HttpResponseMessage httpResponseMessage = await this.httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead);
                httpResponseMessage.EnsureSuccessStatusCode();
                using (var stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                {
                    int readed;

                    do
                    {
                        readed = await stream.ReadAsync(dataArray, 0, dataArray.Length);
                        if (readed > 0)
                        {
                            await outputStream.WriteAsync(dataArray, 0, readed);
                            fileDownloaderItem.PositionOffset += readed;
                            // Read succeded, reset error tracking
                            fileDownloaderItem.nextTry = null;
                            fileDownloaderItem.TryCount = 0;
                        }

                        if (fileDownloaderItem.AbortRequested)
                        {
                            return DownloadStatusEnum.SUSPENDED;
                        }
                    } while (readed > 0);
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "download error from " + fileDownloaderItem.TargetUrl);
                if (fileDownloaderItem.TryCount > CONST_TRYCOUNT)
                {
                    return DownloadStatusEnum.FAILED;
                }
                else
                {
                    fileDownloaderItem.TryCount = fileDownloaderItem.TryCount + 1;
                    fileDownloaderItem.nextTry = DateTime.Now.AddSeconds(5);
                    return DownloadStatusEnum.SUSPENDED;
                }
            }
        }
        return DownloadStatusEnum.COMPLETED;

    }

}



public enum DownloadStatusEnum
{
    SUSPENDED,
    COMPLETED,
    FAILED
}

public record FileDownloaderItem(string TargetUrl, string TargetFile, Action<FileDownloaderItem, DownloadStatusEnum> DownloadCallBack)
{
    public long PositionOffset = 0;
    public volatile bool AbortRequested = false;
    public DateTime LastRunTime = DateTime.MinValue;

    public DateTime? nextTry = null;
    public int TryCount = 0;
}
