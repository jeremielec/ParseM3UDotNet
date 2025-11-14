using System;

namespace ParseM3UNet.StreamUtils;

public class PartialFileStream(string fileName)
{
    public readonly string fileName = fileName;
    public readonly string tempFileName = fileName + ".tmp";

    public readonly byte[] Data = new byte[262140];

    public long CurrentOffset { get; set; } = 0;

    // return true until no data available any more
    public async Task<int?> Read()
    {
        var openFile = GetTargetFile();
        using (openFile.Stream)
        {
            openFile.Stream.Seek(CurrentOffset, SeekOrigin.Begin);
            int result = await openFile.Stream.ReadAsync(Data, 0, Data.Length);
            if (openFile.IsTemp == false && result == 0)
            {
                // EOF
                return null;
            }
            CurrentOffset = CurrentOffset + result;
            return result;
        }
    }

    private (FileStream Stream, bool IsTemp) GetTargetFile()
    {
        if (File.Exists(fileName))
            return (File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), false);

        if (File.Exists(tempFileName))
            return (File.Open(tempFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), true);

        throw new FileNotFoundException("Stream has been closed / cleaned");
    }

    public void UpdateLastAccessTime()
    {
        if (File.Exists(fileName))
            UpdateLastAccessTime(this.fileName);
        if (File.Exists(tempFileName))
            UpdateLastAccessTime(this.tempFileName);
    }
    
    private void UpdateLastAccessTime(string f)
    {
        DateTime nouvelleDate = DateTime.Now;
        File.SetLastAccessTime(f, nouvelleDate);
    }
}
