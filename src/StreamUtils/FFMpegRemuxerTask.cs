using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.Extensions.Logging;
using Models;
using ParseM3UNet.Helpers;
using ParseM3UNet.Http;

namespace ParseM3UNet.StreamUtils;

public class FFMpegRemuxerTask(ILogger<FFMpegRemuxerTask> logger, SettingsModel settingsModel, HttpSingletonClient httpSingletonClient)
{
    private readonly ILogger<FFMpegRemuxerTask> logger = logger;
    private readonly SettingsModel settingsModel = settingsModel;
    private readonly HttpSingletonClient httpSingletonClient = httpSingletonClient;

    public async Task RunRemuxer(MappedBinaryFile mappedBinaryFile)
    {
        logger.LogInformation($"FFMpeg remux starting for URL  : {mappedBinaryFile.Url}");

        ProcessStartInfo processStartInfo = new ProcessStartInfo()
        {
            FileName = "ffmpeg",
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };

        processStartInfo.ArgumentList.Add("-y");
        processStartInfo.ArgumentList.Add("-reconnect_on_network_error");
        processStartInfo.ArgumentList.Add("0");
        processStartInfo.ArgumentList.Add("-i");
        //       processStartInfo.ArgumentList.Add(mappedBinaryFile.LocalFile);

        string proxyUrl = $"http://127.0.0.1:{settingsModel.Http.ListenPort}/proxy/{JsonUtils.SerializeToBase64(mappedBinaryFile.Url)}";
        processStartInfo.ArgumentList.Add(proxyUrl);

        // processStartInfo.ArgumentList.Add("-");

        //processStartInfo.ArgumentList.Add(mappedBinaryFile.Url);
        processStartInfo.ArgumentList.Add("-user_agent");
        processStartInfo.ArgumentList.Add(settingsModel.Http.UserAgent);

        processStartInfo.ArgumentList.Add("-map");
        processStartInfo.ArgumentList.Add("0:v");
        processStartInfo.ArgumentList.Add("-map");
        processStartInfo.ArgumentList.Add("0:a");
        processStartInfo.ArgumentList.Add("-map");
        processStartInfo.ArgumentList.Add("0:s");

        //        processStartInfo.ArgumentList.Add("-sn");
        processStartInfo.ArgumentList.Add("-c:v");
        processStartInfo.ArgumentList.Add("copy");
        processStartInfo.ArgumentList.Add("-c:a");
        processStartInfo.ArgumentList.Add("copy");

        processStartInfo.ArgumentList.Add("-avoid_negative_ts");
        processStartInfo.ArgumentList.Add("make_zero");

        processStartInfo.ArgumentList.Add("-f");
        processStartInfo.ArgumentList.Add("matroska");
        processStartInfo.ArgumentList.Add(mappedBinaryFile.LocalFile);

        Process workingJob = Process.Start(processStartInfo)!;


        using (FileStream log = File.Open(mappedBinaryFile.LocalFFMpegLogFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
            //  workingJob.ErrorDataReceived += (a, b) => OnConsoleRead(a, b, log);
            //  workingJob.OutputDataReceived += (a, b) => OnConsoleRead(a, b, log); ;
            //  workingJob.BeginErrorReadLine();
            //  workingJob.BeginOutputReadLine();

            Task[] allTask = new[] {
                workingJob.StandardError.BaseStream.CopyToAsync(log),
                workingJob.StandardOutput.BaseStream.CopyToAsync(log)
                };

            await Task.WhenAll(allTask);


            while (workingJob.HasExited == false)
            {
                await Task.Delay(500);
            }

            if (workingJob.ExitCode == 0)
            {
                logger.LogInformation($"FFMpeg remux succes for URL  : {mappedBinaryFile.Url}");
                mappedBinaryFile.Metadata.IsCompleted = true;
                await mappedBinaryFile.Metadata.UpdateMetadataAsync();
            }
            else
            {
                logger.LogInformation($"FFMpeg failed with status code : {workingJob.ExitCode}");
            }

        }


    }

    // private async Task OnConsoleRead(object sender, DataReceivedEventArgs e, FileStream fileStream)
    // {
    //     if (e.Data != null)
    //     {
    //         if (e.Data.StartsWith("frame=")) return;
    //         fileStream.Write(Encoding.UTF8.GetBytes(e.Data + "\n"));
    //         fileStream.Flush();
    //         //logger.LogInformation($"FFMpeg Output : " + e.Data);
    //     }

    // }
}
