using System.Buffers;

public class Const
{
    public const long StartupDownloadBlockSize = 1024 * 1000 * 16;
    public const long LiteDownloadBlockSize = 1024 * 1000 * 128;
    public const long HugeDownloadBlockSize = 1024 * 1000 * 512;
    public const int MemoryBlockSize = (int)(1024 * 1000 * 512 * 1.1);

    public static ArrayPool<byte> pool = ArrayPool<byte>.Create();

    public static MemoryStreamReusable CreateMemorySream()
    {
        byte[] d = ArrayPool<byte>.Shared.Rent(MemoryBlockSize);
        return new MemoryStreamReusable(d);
    }
}


public class MemoryStreamReusable : MemoryStream, IDisposable
{
    public readonly byte[] ArrayBackend;

    public MemoryStreamReusable(byte[] array) : base(array, 0, array.Length)
    {
        this.ArrayBackend = array;
        this.SetLength(array.Length);
    }

    public new void Dispose()
    {
        Const.pool.Return(ArrayBackend);
    }
}