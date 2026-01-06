using System;

namespace ParseM3UNet.StreamUtils;

public class StreamCopy(Stream source)
{
    private readonly Stream source = source;

    public async Task<MemoryStreamReusable?> Copy(long? count)
    {
        MemoryStreamReusable? stream = Const.CreateMemorySream();


        long remaining = count ?? long.MaxValue;
        long copiedByte = 0;

        while (remaining > 0)
        {


            int readResult;
            try
            {
                long toRead = Math.Min(stream.ArrayBackend.Length, remaining);
                readResult = await source.ReadAsync(stream.ArrayBackend, (int)copiedByte, (int)toRead);

                copiedByte += readResult;
                remaining -= readResult;


                if (readResult == 0)
                    break;
            }
            catch (Exception e)
            {
                if (e.Message.Contains("prematurely"))
                {
                    break;
                }
                else
                {
                    throw;
                }
            }

        }
        stream.Position = 0;
        stream.SetLength(copiedByte);
        return stream;

    }
}
