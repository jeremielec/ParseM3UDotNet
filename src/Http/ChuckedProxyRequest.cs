using System;
using System.Buffers;
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


        if (httpContext.Request.Headers.Range.Any())
        {
            var rangeRequest = httpContext.Request.Headers.Range.First()!;
            var rangeValue = RangeHeaderValue.Parse(rangeRequest);
            start = rangeValue.Ranges.First().From.GetValueOrDefault();
            end = rangeValue.Ranges.First().To ?? long.MaxValue;
        }

        long currentOffset = start;


        while (currentOffset < end)
        {
            long endRequest = Math.Min(currentOffset + Const.DefaultBlockSize, end);
            long length = endRequest - currentOffset;
            MemoryStreamReusable? data = null;
            bool result = await httpSingletonClient.DownloadBlock(targetUrl, currentOffset, endRequest, async result =>
               {
                   if (currentOffset == start)
                   {
                       SetHeadersFromContentRange(httpContext, result);
                       if (currentOffset == 0)
                       {
                           httpContext.Response.Headers.ContentLength = result.Content.Headers.ContentLength;
                       }
                   }

                   var stream = await result.Content.ReadAsStreamAsync();
                   data = await new StreamCopy(stream).Copy(length);

               });

            if (data != null)
            {
                using (data)
                {

                    var toWrite = new ReadOnlyMemory<byte>(data.ArrayBackend, 0, (int)data.Length);
                    await httpContext.Response.BodyWriter.WriteAsync(toWrite);
                    currentOffset += data.Length;
                }
            }

            if (result == false) break;
            if (httpContext.RequestAborted.IsCancellationRequested) break;
        }


    }

    private void SetHeadersFromContentRange(HttpContext httpContext, HttpResponseMessage result)
    {
        httpContext.Response.ContentType = result.Content.Headers.ContentType!.ToString();
        httpContext.Response.Headers.ContentRange = new Microsoft.Extensions.Primitives.StringValues(result.Content.Headers.ContentRange?.ToString());
        httpContext.Response.Headers.AcceptRanges = new Microsoft.Extensions.Primitives.StringValues("bytes");
    }
}
