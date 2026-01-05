using System.Text.Json;
using Helpers;
using Models;

namespace ParseM3UNet.StreamUtils;

public class MappedBinaryFileRepository
{
    private readonly KnownDirectory knownDirectory;
    private readonly SettingsModel settingsModel1;
    private List<MappedBinaryFile> OpenedFiles = new();

    public MappedBinaryFileRepository(KnownDirectory knownDirectory, SettingsModel settingsModel1)
    {
        this.knownDirectory = knownDirectory;
        this.settingsModel1 = settingsModel1;
        OpenExistingFile();
    }


    private void OpenExistingFile()
    {
        foreach (var meta in Directory.EnumerateFiles(knownDirectory.pathCacheDir))
        {
            if (meta.EndsWith(".metadata"))
            {
                string json = File.ReadAllText(meta);
                var instance = new MappedBinaryFile(knownDirectory, JsonSerializer.Deserialize<MappedBinaryFile.MappedBinaryFileMetadata>(json)!);
                instance.Metadata.MetadataLocation = meta;
                OpenedFiles.Add(instance);
            }
        }
        CleanUp();

    }

    public void CleanUp()
    {
        lock (OpenedFiles)
        {
            while (OpenedFiles.Count > settingsModel1.Output.MaxCacheItem)
            {
                var toDelte = OpenedFiles.OrderBy(a => a.Metadata.LastAccess).First();

                toDelte.Delete();
                OpenedFiles.Remove(toDelte);
            }

        }
    }


    public MappedBinaryFile Open(string Url)
    {
        lock (OpenedFiles)
        {
            var query = OpenedFiles.Where(a => a.Url == Url);
            if (query.Any())
            {
                var i = query.First();
                i.Metadata.LastAccess = DateTime.Now;
                i.Metadata.UpdateMetadata();

                return query.Single();
            }

            var instance = new MappedBinaryFile(knownDirectory, Url)
            {

            };
            OpenedFiles.Add(instance);

            CleanUp();
            instance.Metadata.LastAccess = DateTime.Now;
            instance.Metadata.UpdateMetadata();


            return instance;
        }
    }

}
