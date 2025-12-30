using System;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace ParseM3UNet.Helpers;

public static class HttpServeFile
{
    public static async Task ServeFileAsync(
        HttpContext context,
        string filePath,
        string contentType = "application/octet-stream")
    {
        FileInfo file = new FileInfo(filePath);
        if (!file.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        long fileLength = file.Length;
        context.Response.Headers.AcceptRanges = "bytes";
        context.Response.ContentType = contentType;
        context.Response.Headers.ContentDisposition = "inline";

        long start = 0;
        long end = fileLength - 1;

        // ==== PARSE RANGE ====
        if (context.Request.Headers.TryGetValue("Range", out var rangeHeader))
        {
            if (!RangeHeaderValue.TryParse(rangeHeader, out var range) ||
                range.Ranges.Count != 1)
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers.ContentRange = $"bytes */{fileLength}";
                return;
            }

            var r = range.Ranges.First();

            if (r.From.HasValue)
                start = r.From.Value;

            if (r.To.HasValue)
                end = r.To.Value;

            if (start >= fileLength || end >= fileLength || start > end)
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers.ContentRange = $"bytes */{fileLength}";
                return;
            }

            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers.ContentRange =
                $"bytes {start}-{end}/{fileLength}";
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
        }

        long contentLength = end - start + 1;
        context.Response.ContentLength = contentLength;

        if (context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            return;

        // ==== STREAM ====
        const int bufferSize = 64 * 1024;
        byte[] buffer = new byte[bufferSize];

        using FileStream fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize,
            FileOptions.SequentialScan);

        fs.Seek(start, SeekOrigin.Begin);

        long remaining = contentLength;

        while (remaining > 0 && !context.RequestAborted.IsCancellationRequested)
        {
            int read = await fs.ReadAsync(
                buffer, 0,
                (int)Math.Min(buffer.Length, remaining),
                context.RequestAborted);

            if (read == 0)
                break;

            await context.Response.Body.WriteAsync(
                buffer, 0, read,
                context.RequestAborted);

            remaining -= read;
        }

        await context.Response.Body.FlushAsync();
    }
}
