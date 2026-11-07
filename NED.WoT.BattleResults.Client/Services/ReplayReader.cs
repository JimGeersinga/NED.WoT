using System.Buffers.Binary;
using System.Diagnostics;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>
/// Reads the JSON blocks of a .wotreplay: <c>uint32 magic 0x11343212</c> | <c>uint32 blockCount</c> | blockCount × (<c>uint32 length</c> + UTF-8 JSON) | encrypted binary.
/// Block 0 is the header, block 1 the results. Nothing after block 1 is read.
/// </summary>
public static class ReplayReader
{
    private const uint MAGIC = 0x11343212;

    /// <summary>The header block and, when the replay has one, the results block (null for a replay that ended without results).</summary>
    public sealed record ReplayBlocks(byte[] Header, byte[]? Results);

#if DEBUG
    private static long _bytesRead;

    /// <summary>Debug build only: the bytes the readers have consumed since <see cref="ResetBytesRead"/>.</summary>
    public static long BytesRead => Interlocked.Read(ref _bytesRead);

    public static void ResetBytesRead() => Interlocked.Exchange(ref _bytesRead, 0);
#endif

    /// <exception cref="InvalidDataException">Wrong magic, no blocks, or a block that runs past the end of the file.</exception>
    public static ReplayBlocks Read(string path)
    {
        // bufferSize 0: no read-ahead, so the file system is asked for the blocks and not for the 4 KB around them.
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0);

        return Read(stream);
    }

    /// <exception cref="InvalidDataException">Wrong magic, no blocks, or a block that runs past the end of the stream.</exception>
    public static ReplayBlocks Read(Stream stream)
    {
        long start = stream.Position;

        try
        {
            Span<byte> head = stackalloc byte[8];
            if (stream.Read(head) != head.Length)
            {
                throw new InvalidDataException("The replay is shorter than its header.");
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(head) != MAGIC)
            {
                throw new InvalidDataException("Not a replay: wrong magic.");
            }

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(head[4..]);
            if (count == 0)
            {
                throw new InvalidDataException("The replay has no blocks.");
            }

            byte[] header = ReadBlock(stream);
            byte[]? results = count > 1 ? ReadBlock(stream) : null;

            return new ReplayBlocks(header, results);
        }
        finally
        {
#if DEBUG
            Interlocked.Add(ref _bytesRead, stream.Position - start);
#endif
        }
    }

    private static byte[] ReadBlock(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        if (stream.Read(lengthBytes) != lengthBytes.Length)
        {
            throw new InvalidDataException("The replay ends inside a block length.");
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length > stream.Length - stream.Position)
        {
            throw new InvalidDataException("A block is longer than the rest of the replay.");
        }

        byte[] block = new byte[length];
        stream.ReadExactly(block);

        return block;
    }

    [Conditional("DEBUG")]
    public static void SelfCheck()
    {
        byte[] a = [(byte)'a'];
        byte[] bc = "{}"u8.ToArray();

        ReplayBlocks one = Read(Replay(MAGIC, a));
        Debug.Assert(one.Header.SequenceEqual(a) && one.Results is null);

        ReplayBlocks two = Read(Replay(MAGIC, a, bc));
        Debug.Assert(two.Header.SequenceEqual(a) && two.Results!.SequenceEqual(bc));

        // Block 2 is never returned, and not read either: the position stays right behind block 1.
        MemoryStream three = Replay(MAGIC, a, bc, [1, 2, 3]);
        ReplayBlocks threeBlocks = Read(three);
        Debug.Assert(threeBlocks.Results!.SequenceEqual(bc) && three.Position == 8 + 4 + a.Length + 4 + bc.Length);

        Debug.Assert(Throws(Replay(0xDEADBEEF, a)));
        Debug.Assert(Throws(new MemoryStream(BitConverter.GetBytes(MAGIC)))); // no count
        Debug.Assert(Throws(Replay(MAGIC)));                                  // count 0

        MemoryStream tooLong = Replay(MAGIC, a);
        BinaryPrimitives.WriteUInt32LittleEndian(tooLong.GetBuffer().AsSpan(8), 99);
        Debug.Assert(Throws(tooLong));

        static bool Throws(Stream stream)
        {
            try
            {
                Read(stream);
                return false;
            }
            catch (InvalidDataException)
            {
                return true;
            }
        }

        static MemoryStream Replay(uint magic, params byte[][] blocks)
        {
            MemoryStream stream = new();
            using (BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(magic);
                writer.Write((uint)blocks.Length);
                foreach (byte[] block in blocks)
                {
                    writer.Write((uint)block.Length);
                    writer.Write(block);
                }
            }

            stream.Position = 0;

            return stream;
        }
    }
}
