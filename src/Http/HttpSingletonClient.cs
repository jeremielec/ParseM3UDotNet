using System;
using System.Net;
using Microsoft.AspNetCore.Routing.Tree;
using Models;
using ParseM3UNet.StreamUtils;

namespace ParseM3UNet.Http;

public class HttpSingletonClient(SettingsModel settingsModel)
{
    private readonly SettingsModel settingsModel = settingsModel;
    HttpClient httpClient = new();
    SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);


    public async Task<bool> DownloadBlock(string url, long start, long end, Func<HttpResponseMessage, Task> callback, int tryCuount = 10)
    {

        if (tryCuount == 0) throw new TimeoutException("failed to recover from 509");

        await semaphoreSlim.WaitAsync();
        HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", settingsModel.Http.UserAgent);

        if (start != 0 && end != long.MaxValue)
            httpRequestMessage.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

        try
        {
            try
            {
                using (var response = await httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                        return false;

                    if ((int)response.StatusCode == 509)
                    {
                        await Task.Delay(60000);
                        return await DownloadBlock(url, start, end, callback, tryCuount--);
                    }
                    response.EnsureSuccessStatusCode();
                    await callback(response);
                    return true;
                    // var stream = await response.Content.ReadAsStreamAsync();
                    // MemoryStream memoryStream = new MemoryStream();
                    // await stream.CopyToAsync(memoryStream);
                    // return (memoryStream, response);
                }
            }
            catch (Exception e)
            {

                if (e.Message.Contains("reset"))
                {
                    return false; // WOrkarrou for bad server response
                }
                throw;
            }

        }
        finally
        {
            semaphoreSlim.Release();
        }
    }


}
