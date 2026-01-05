using System;

namespace ParseM3UNet.StreamUtils;

public class StreamCopy(Stream source, Stream dest)
{
    private readonly Stream source = source;
    private readonly Stream dest = dest;
    byte[] data = new byte[65535];

    public async Task<long> Copy(long count)
    {
        long remaining = count;
        long copiedByte = 0;
        while (remaining > 0)
        {

            int readResult = 0;
            try
            {
                readResult = await source.ReadAsync(data, 0, data.Length);
            }
            catch (Exception e)
            {
                if (e.Message.Contains("prematurely"))
                {
                    break;
                }
            }

            if (readResult > 0)
            {
                long toWrite = Math.Min(remaining, readResult);

                await dest.WriteAsync(data, 0, (int)toWrite);
                await dest.FlushAsync();
                copiedByte += toWrite;
                remaining -= toWrite;
            }

        }
        return copiedByte;
    }
}
