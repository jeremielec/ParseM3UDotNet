using System.Buffers;

public class Const
{
    public const long DefaultBlockSize = 1024 * 1000 * 64;

    public static ArrayPool<byte> pool = ArrayPool<byte>.Create();

    public static MemoryStreamReusable CreateMemorySream()
    {
        byte[] d = pool.Rent((int)DefaultBlockSize);
        return new MemoryStreamReusable(d);
    }
}


public class MemoryStreamReusable : MemoryStream, IDisposable
{
    public readonly byte[] ArrayBackend;

    public MemoryStreamReusable(byte[] array) : base(array, 0, array.Length )
    {
        this.ArrayBackend = array;
       // this.SetLength(array.Length);
    }

    public new void Dispose()
    {
        Const.pool.Return(ArrayBackend);
    }
}