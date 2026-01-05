using System;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Helpers;
using ParseM3UNet.Helpers;

namespace ParseM3UNet.StreamUtils;

public class MappedBinaryFile
{
    public readonly MappedBinaryFileMetadata Metadata;


    private readonly KnownDirectory knownDirectory;
    public readonly string Url;


    public MappedBinaryFile(KnownDirectory KnownDirectory, string Url)
    {
        knownDirectory = KnownDirectory;
        this.Url = Url;



        if (File.Exists(LocalMapFile) == false)
        {
            Metadata = new(Url)
            {
                LastAccess = DateTime.Now,
                MetadataLocation = LocalMapFile
            };
            File.WriteAllText(LocalMapFile, JsonSerializer.Serialize(Metadata));
        }
        else
        {
            string content = File.ReadAllText(LocalMapFile);
            Metadata = JsonSerializer.Deserialize<MappedBinaryFileMetadata>(content)!;
            Metadata.MetadataLocation = LocalMapFile;
        }
    }


    public MappedBinaryFile(KnownDirectory KnownDirectory, MappedBinaryFileMetadata Metadata)
    {
        knownDirectory = KnownDirectory;
        this.Url = Metadata.Url;
        this.Metadata = Metadata;
    }

    public FileStream OpenFile()
    {
        return File.Exists(LocalFile) == false ?
                   File.Open(LocalFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite)
                 : File.Open(LocalFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    }


    public void Delete()
    {
        if (File.Exists(LocalFile))
            File.Delete(LocalFile);
        if (File.Exists(LocalMapFile))
            File.Delete(LocalFile);
        if (File.Exists(LocalFFMpegLogFile))
            File.Delete(LocalFFMpegLogFile);
        if (File.Exists(Metadata.MetadataLocation))
            File.Delete(Metadata.MetadataLocation);
    }

    public async Task SafeFileAccess(Func<FileStream, Task> callback)
    {
        //  if (Fd == null) throw new ObjectDisposedException("Disposed FileStream");
        try
        {

            var Fd = OpenFile();
            using (Fd)
            {
                await callback(Fd);
            }
        }
        finally
        {
        }

    }



    public string LocalFile => Path.Combine(knownDirectory.pathCacheDir, SanitizeUrl(Url)) + ".mkv";
    public string LocalFFMpegLogFile => Path.Combine(knownDirectory.pathCacheDir, SanitizeUrl(Url) + "ffmpeg.log");
    private string LocalMapFile => Path.Combine(knownDirectory.pathCacheDir, SanitizeUrl(Url)) + ".metadata";
    private static string SanitizeUrl(string url) => url.Replace('/', '_').Replace(':', '_');



    public record MappedBinaryFileMetadata(string Url)
    {

        public bool IsCompleted { get; set; }
        public DateTime LastAccess { get; set; }

        public string MetadataLocation = default!;


        public async Task UpdateMetadataAsync()
        {
            await File.WriteAllTextAsync(MetadataLocation, JsonSerializer.Serialize(this));
        }
        public void UpdateMetadata()
        {
            File.WriteAllText(MetadataLocation, JsonSerializer.Serialize(this));
        }


    }


}
