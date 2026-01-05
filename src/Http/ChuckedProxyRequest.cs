using System;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using ParseM3UNet.StreamUtils;

namespace ParseM3UNet.Http;

public class ChuckedProxyRequest(HttpSingletonClient httpSingletonClient)
{
    private readonly HttpSingletonClient httpSingletonClient = httpSingletonClient;

    public async Task ProxyRequest(HttpContext httpContext, string targetUrl)
    {

        long start = 0;
        long end = long.MaxValue;
        long currentOffset = start;
        const long blockSize = 1024 * 1000 * 32;


        if (httpContext.Request.Headers.Range.Any())
        {
            var rangeRequest = httpContext.Request.Headers.Range.First()!;
            var rangeValue = RangeHeaderValue.Parse(rangeRequest);
            start = rangeValue.Ranges.First().From.GetValueOrDefault();
            end = rangeValue.Ranges.First().To ?? long.MaxValue;
        }
        while (currentOffset < end)
        {
            long endRequest = Math.Min(currentOffset + blockSize, end);
            long length = endRequest - currentOffset;

            bool requestOk = await httpSingletonClient.DownloadBlock(targetUrl, currentOffset, endRequest, async result =>
             {
                 if (currentOffset == start)
                 {
                     SetHeadersFromContentRange(httpContext, result);
                 }

                 var stream = await result.Content.ReadAsStreamAsync();
                 long copied = await Task.Run(async () => await new StreamCopy(stream, httpContext.Response.Body).Copy(length));
                 currentOffset += copied;
             });

            if (requestOk == false) break;
            else await Task.Delay(500);

        }


    }

    private void SetHeadersFromContentRange(HttpContext httpContext, HttpResponseMessage result)
    {
        httpContext.Response.ContentType = result.Content.Headers.ContentType!.ToString();
        httpContext.Response.Headers.ContentRange = new Microsoft.Extensions.Primitives.StringValues(result.Content.Headers.ContentRange?.ToString());
        httpContext.Response.Headers.AcceptRanges = new Microsoft.Extensions.Primitives.StringValues("bytes");
    }
}
