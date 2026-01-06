

using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Models;
using ParseM3UNet.Helpers;
using ParseM3UNet.StreamUtils;

namespace ParseM3UNet.Http
{
    public static class HttpRequestHandler
    {





        public static async Task HandleRequest(HttpContext context)
        {
            var matchProxy = context.RequestServices.GetRequiredService<RegexRepository>().HttpUrlRegexProxy.Match(context.Request.Path);
            var match = context.RequestServices.GetRequiredService<RegexRepository>().HttpUrlRegex.Match(context.Request.Path);

            if (matchProxy.Success)
            {
                await HandleProxyRequest(context, matchProxy);
            }
            else if (match.Success)
            {
                await HandleStrmRequest(context, match);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

        }

        private static async Task HandleStrmRequest(HttpContext context, System.Text.RegularExpressions.Match match)
        {
            string data = match.Groups.Values.Last().Value;
            string targetUrl = JsonUtils.DeserializeFromBase64<string>(data);

            MappedBinaryFileRepository localFileSync = context.RequestServices.GetRequiredService<MappedBinaryFileRepository>();
            var mappedFIle = localFileSync.Open(targetUrl);

            if (mappedFIle.Metadata.IsCompleted == false)
            {
                FFMpegRemuxerOrchestor ffMpegRemuxer = context.RequestServices.GetRequiredService<FFMpegRemuxerOrchestor>();
                ffMpegRemuxer.AddSyncUrl(mappedFIle);

                Stopwatch stopwatch = Stopwatch.StartNew();
                while (File.Exists(mappedFIle.LocalFile) == false)
                {
                    await Task.Delay(100);
                    if (context.RequestAborted.IsCancellationRequested)
                    {
                        await context.Response.CompleteAsync();
                        break;
                    }

                    if (stopwatch.Elapsed.TotalSeconds > 20) throw new TimeoutException("timeout waiting for file exists : " + mappedFIle.LocalFile);
                }
            }
            await ServeMappedBinaryFileAsync(context, mappedFIle);
        }

        private static async Task HandleProxyRequest(HttpContext context, System.Text.RegularExpressions.Match matchProxy)
        {
            string data = matchProxy.Groups.Values.Last().Value;
            string targetUrl = JsonUtils.DeserializeFromBase64<string>(data);
            ChuckedProxyRequest chuckedProxyRequest = context.RequestServices.GetRequiredService<ChuckedProxyRequest>();
            await chuckedProxyRequest.ProxyRequest(context, targetUrl);
        }




        public static async Task ServeMappedBinaryFileAsync(
               HttpContext context,
               MappedBinaryFile mappedFile,
               int bufferSize = 64 * 1024)
        {
            var request = context.Request;
            var response = context.Response;
            var metadata = mappedFile.Metadata;

            response.ContentType = " video/x-matroska";
            long start = 0;
            long? end = null;

            if (mappedFile.Metadata.IsCompleted)
            {
                var rangeRequest = request.Headers.Range.FirstOrDefault();

                if (rangeRequest != null)
                {
                    var rangeValue = RangeHeaderValue.Parse(rangeRequest);
                    start = rangeValue.Ranges.First().From.GetValueOrDefault();
                    end = rangeValue.Ranges.First().To;
                }

                response.Headers.AcceptRanges = $"bytes";
                response.Headers.ContentLength = new FileInfo(mappedFile.LocalFile).Length;
                
            }

            long remaining = (end ?? long.MaxValue) - start;
            long currentPosition = start;

            while (remaining > 0)
            {
                if (context.RequestAborted.IsCancellationRequested)
                {
                    break;
                }
                long lastWriteResult = 0;
                await mappedFile.SafeFileAccess(async a =>
                {
                    a.Seek(currentPosition, SeekOrigin.Begin);

                    var streamCopy = new StreamCopy(a);
                    var data = await streamCopy.Copy(Math.Min(remaining, Const.DefaultBlockSize));
                    if (data != null)
                    {
                        lastWriteResult = data.Length;
                        using (data)
                        {
                            ReadOnlyMemory<byte> readOnlyMemory = new ReadOnlyMemory<byte>(data.ArrayBackend, 0, (int)data.Length);
                            await response.BodyWriter.WriteAsync(readOnlyMemory);
                            remaining -= data.Length;
                            currentPosition += data.Length;
                        }
                    }
                });

                if (mappedFile.Metadata.IsCompleted && lastWriteResult == 0)
                    break;
                else
                    if (lastWriteResult == 0)
                        await Task.Delay(1000);
            }
        }


    }
}