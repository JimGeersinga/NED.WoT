using System.Buffers;
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

    /// <summary>
    /// The header block and, when the replay has one, the results block. Both live in buffers rented from <see cref="ArrayPool{T}.Shared"/>:
    /// dispose this when the blocks are parsed, and keep nothing that points into <see cref="Header"/> or <see cref="Results"/> after that.
    /// </summary>
    public sealed class ReplayBlocks : IDisposable
    {
        private byte[]? _header;
        private byte[]? _results;
        private readonly int _headerLength;
        private readonly int _resultsLength;

        internal ReplayBlocks(byte[] header, int headerLength, byte[]? results, int resultsLength)
        {
            _header = header;
            _headerLength = headerLength;
            _results = results;
            _resultsLength = resultsLength;
        }

        /// <summary>The block's real bytes (the rented buffer is longer).</summary>
        public ReadOnlyMemory<byte> Header => _header.AsMemory(0, _headerLength);

        /// <summary>The block's real bytes; empty when <see cref="HasResults"/> is false.</summary>
        public ReadOnlyMemory<byte> Results => _results.AsMemory(0, _resultsLength);

        /// <summary>False for a replay that ended without results (or whose results block is damaged).</summary>
        public bool HasResults => _results is not null;

        public void Dispose()
        {
            byte[]? header = Interlocked.Exchange(ref _header, null);
            byte[]? results = Interlocked.Exchange(ref _results, null);
            if (header is not null)
            {
                ArrayPool<byte>.Shared.Return(header);
            }

            if (results is not null)
            {
                ArrayPool<byte>.Shared.Return(results);
            }
        }
    }

    /// <exception cref="InvalidDataException">Wrong magic, no blocks, or a header block that runs past the end of the file.</exception>
    public static ReplayBlocks Read(string path)
    {
        // bufferSize 0: no read-ahead, so the file system is asked for the blocks and not for the 4 KB around them.
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0);

        return Read(stream);
    }

    /// <exception cref="InvalidDataException">Wrong magic, no blocks, or a header block that runs past the end of the stream. A damaged results block gives <c>Results = null</c>.</exception>
    public static ReplayBlocks Read(Stream stream)
    {
        Span<byte> head = stackalloc byte[8];
        if (stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) != head.Length)
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

        (byte[] Buffer, int Length)? header = ReadBlock(stream) ?? throw new InvalidDataException("The header block is missing or runs past the end of the replay.");
        (byte[] Buffer, int Length)? results;
        try
        {
            results = count > 1 ? ReadBlock(stream) : null;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(header.Value.Buffer);
            throw;
        }

        return new ReplayBlocks(header.Value.Buffer, header.Value.Length, results?.Buffer, results?.Length ?? 0);
    }

    /// <returns>A rented buffer (longer than the block) with the block's length, or null when its length field is missing or the block runs past the end of the stream.</returns>
    private static (byte[] Buffer, int Length)? ReadBlock(Stream stream)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        if (stream.ReadAtLeast(lengthBytes, lengthBytes.Length, throwOnEndOfStream: false) != lengthBytes.Length)
        {
            return null;
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length > stream.Length - stream.Position)
        {
            return null;
        }

        byte[] block = ArrayPool<byte>.Shared.Rent((int)length);
        try
        {
            stream.ReadExactly(block.AsSpan(0, (int)length));
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(block);
            throw;
        }

        return (block, (int)length);
    }

    [Conditional("DEBUG")]
    public static void SelfCheck()
    {
        byte[] a = [(byte)'a'];
        byte[] bc = "{}"u8.ToArray();

        using ReplayBlocks one = Read(Replay(MAGIC, a));
        Debug.Assert(one.Header.Span.SequenceEqual(a) && !one.HasResults);

        using ReplayBlocks two = Read(Replay(MAGIC, a, bc));
        Debug.Assert(two.Header.Span.SequenceEqual(a) && two.Results.Span.SequenceEqual(bc));

        // Block 2 is never returned, and not read either: the position stays right behind block 1.
        MemoryStream three = Replay(MAGIC, a, bc, [1, 2, 3]);
        using ReplayBlocks threeBlocks = Read(three);
        Debug.Assert(threeBlocks.Results.Span.SequenceEqual(bc) && three.Position == 8 + 4 + a.Length + 4 + bc.Length);

        Debug.Assert(Throws(Replay(0xDEADBEEF, a)));
        Debug.Assert(Throws(new MemoryStream(BitConverter.GetBytes(MAGIC)))); // no count
        Debug.Assert(Throws(Replay(MAGIC)));                                  // count 0

        // A header block past the end is fatal; a results block past the end (or without its length field) is only "no results".
        MemoryStream headerTooLong = Replay(MAGIC, a);
        BinaryPrimitives.WriteUInt32LittleEndian(headerTooLong.GetBuffer().AsSpan(8), 99);
        Debug.Assert(Throws(headerTooLong));

        MemoryStream resultsTooLong = Replay(MAGIC, a, bc);
        BinaryPrimitives.WriteUInt32LittleEndian(resultsTooLong.GetBuffer().AsSpan(8 + 4 + a.Length), 99);
        using ReplayBlocks damaged = Read(resultsTooLong);
        Debug.Assert(damaged.Header.Span.SequenceEqual(a) && !damaged.HasResults);

        MemoryStream lengthMissing = Replay(MAGIC, a, bc);
        lengthMissing.SetLength(8 + 4 + a.Length + 2); // the count says 2, block 1 has half a length field
        using ReplayBlocks missing = Read(lengthMissing);
        Debug.Assert(!missing.HasResults);

        // Pooled path: a block longer than a rented bucket start (so the buffer is longer than the block) comes back sliced to its real length, and a recycled dirty buffer does not leak into the next read.
        byte[] big = new byte[100];
        Array.Fill(big, (byte)'x');
        byte[] small = [(byte)'y'];
        using (ReplayBlocks first = Read(Replay(MAGIC, big, big)))
        {
            Debug.Assert(first.Header.Length == 100 && first.Results.Length == 100);
        }

        using ReplayBlocks second = Read(Replay(MAGIC, small, small));
        Debug.Assert(second.Header.Span.SequenceEqual(small) && second.Results.Span.SequenceEqual(small));

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
