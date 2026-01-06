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
            long? endRequest;
            MemoryStreamReusable? data = null;

            if (currentOffset > Const.StartupDownloadBlockSize
                 && end == long.MaxValue
                 && httpSingletonClient.IsBusy() == false)
            {
                endRequest = null;
            }
            else
            {
                long initialBlock = currentOffset == 0 ? Const.StartupDownloadBlockSize : Const.LiteDownloadBlockSize;
                endRequest = Math.Min(currentOffset +initialBlock, end);
            }


            bool result = await httpSingletonClient.DownloadBlock(targetUrl, currentOffset, endRequest, async result =>
               {
                   if (currentOffset == start)
                   {
                       SetHeadersFromContentRange(httpContext, result);
                       if (currentOffset == 0)
                       {
                           if (result.Content.Headers.ContentRange != null && result.Content.Headers.ContentRange.Length != null)
                           {
                               httpContext.Response.Headers.ContentLength = result.Content.Headers.ContentRange.Length.Value;
                           }
                       }
                   }

                   var stream = await result.Content.ReadAsStreamAsync();

                   if (endRequest != null)
                   {
                       long length = endRequest.Value - currentOffset;
                       data = await new StreamCopy(stream).Copy(length);
                   }
                   else
                   {
                       byte[] data = new byte[65535];
                       long lastRead = 0;

                       long writed = 0;
                       do
                       {
                           lastRead = await stream.ReadAsync(data, 0, data.Length);
                           if (lastRead > 0)
                           {
                               await DoPartialWriteIfNeeded(httpContext, lastRead, data);
                               currentOffset += lastRead;
                               writed += lastRead;
                           }

                           if (httpSingletonClient.HasWaitingThread()) break;

                       } while (lastRead > 0);
                   }

               });

            if (data != null)
            {
                using (data)
                {
                    await DoPartialWriteIfNeeded(httpContext, data.Length, data.ArrayBackend);
                    currentOffset += data.Length;
                }
            }

            if (result == false) break;
            if (httpContext.RequestAborted.IsCancellationRequested) break;
        }


    }

    private static async Task DoPartialWriteIfNeeded(HttpContext httpContext, long length, byte[] data)
    {
        if (length < data.Length)
        {
            var toWrite = new ReadOnlyMemory<byte>(data, 0, (int)length);
            await httpContext.Response.BodyWriter.WriteAsync(toWrite);
        }
        else
        {
            await httpContext.Response.BodyWriter.WriteAsync(data);
        }
    }

    private void SetHeadersFromContentRange(HttpContext httpContext, HttpResponseMessage result)
    {
        httpContext.Response.ContentType = result.Content.Headers.ContentType!.ToString();
        httpContext.Response.Headers.ContentRange = new Microsoft.Extensions.Primitives.StringValues(result.Content.Headers.ContentRange?.ToString());
        httpContext.Response.Headers.AcceptRanges = new Microsoft.Extensions.Primitives.StringValues("bytes");
    }
}
