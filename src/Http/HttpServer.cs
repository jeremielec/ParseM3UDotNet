

using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;
using ParseM3UNet.Helpers;

namespace ParseM3UNet.Http
{
    public static class HttpRequestHandler
    {





        public static async Task HandleRequest(HttpContext context)
        {
            var match = context.RequestServices.GetRequiredService<RegexRepository>().HttpUrlRegex.Match(context.Request.Path);
            if (match.Success)
            {
                string data = match.Groups.Values.Last().Value;
                string targetUrl = JsonUtils.DeserializeFromBase64<string>(data);

                LocalFileSync localFileSync = context.RequestServices.GetRequiredService<LocalFileSync>();
                await localFileSync.SyncAndServerFile(context, targetUrl);
            }
            else
            {
                context.Response.StatusCode = 404;
            }

        }




    }
}