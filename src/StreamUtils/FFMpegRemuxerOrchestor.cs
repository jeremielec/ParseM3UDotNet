using System.ComponentModel;
using System.Net;
using Microsoft.Extensions.Logging;

namespace ParseM3UNet.StreamUtils;

public class FFMpegRemuxerOrchestor(ILogger<FFMpegRemuxerOrchestor> logger, FFMpegRemuxerTask fFMpegRemuxerTask)
{
    private readonly ILogger<FFMpegRemuxerOrchestor> logger = logger;
    private readonly FFMpegRemuxerTask fFMpegRemuxerTask = fFMpegRemuxerTask;
    private List<MappedBinaryFile> Jobs = new();

    // private Task? WorkingJob = null;


    // private async Task DoWork()
    // {
    //     while (true)
    //     {
    //         MappedBinaryFile? toRun;
    //         lock (Jobs)
    //         {
    //             toRun = Jobs.FirstOrDefault();
    //         }

    //         if (toRun != null)
    //         {
    //             try
    //             {
    //                 await fFMpegRemuxerTask.RunRemuxer(toRun);
    //             }
    //             catch (Exception e)
    //             {
    //                 logger.LogCritical(message: "fatal error", exception: e);
    //             }

    //             lock (Jobs)
    //                 Jobs.Remove(toRun);
    //         }
    //     }

    // }

    public void AddSyncUrl(MappedBinaryFile mappedBinaryFile)
    {
        lock (Jobs)
        {
            if (Jobs.Any(a => a.Url == mappedBinaryFile.Url)) return;
            Jobs.Add(mappedBinaryFile);

            Task.Run(async () =>
            {
                try
                {
                    await fFMpegRemuxerTask.RunRemuxer(mappedBinaryFile);
                }
                catch (Exception e)
                {
                    logger.LogCritical(message: "fatal error", exception: e);
                }

                lock (Jobs)
                {
                    Jobs.Remove(mappedBinaryFile);
                }
            });
        }
    }

}
