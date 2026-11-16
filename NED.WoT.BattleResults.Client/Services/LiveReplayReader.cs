using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>Reads the two clan tags from a live temp.wotreplay. That file has no JSON header (it is the raw packet stream); the tags sit in a pickled <c>opponents</c> dict near the start. Never unpickles: the file is untrusted input.</summary>
public static class LiveReplayReader
{
    // The block sits at ~0.7-0.9 KB in every sample (temp3-6, 1.x and 2.x).
    private const int MaxRead = 8 * 1024;
    private const int MaxWalk = 256;
    private static readonly SearchValues<byte> TagChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_"u8);

    /// <returns>The clan tags of team 1 and team 2, or null while the block is not there (yet) or is not the known shape.</returns>
    public static (string, string)? ReadClans(string path)
    {
        using SafeFileHandle file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        Span<byte> buffer = stackalloc byte[MaxRead];
        // A short read (the game is still writing) just means: not there yet, the next tick tries again.
        return Parse(buffer[..RandomAccess.Read(file, buffer, 0)]);
    }

    internal static (string, string)? Parse(ReadOnlySpan<byte> data)
    {
        int key = data.IndexOf("opponents"u8);
        if (key < 0)
        {
            return null;
        }

        ReadOnlySpan<byte> p = data[(key + 9)..];
        ReadOnlySpan<byte> tag1 = default, tag2 = default;
        int team = 0;
        int i = 0;
        while (true)
        {
            if (i >= p.Length || i >= MaxWalk)
            {
                return null;
            }

            switch (p[i++])
            {
                case (byte)'.':
                    return Valid(tag1) && Valid(tag2) ? (Encoding.ASCII.GetString(tag1), Encoding.ASCII.GetString(tag2)) : null;
                case (byte)'U':
                    if (i >= p.Length || i + 1 + p[i] > p.Length)
                    {
                        return null;
                    }
                    ReadOnlySpan<byte> s = p.Slice(i + 1, p[i]);
                    i += 1 + s.Length;
                    if (s.SequenceEqual("1"u8)) team = 1;
                    else if (s.SequenceEqual("2"u8)) team = 2;
                    else if (team > 0 && !s.SequenceEqual("name"u8))
                    {
                        // The first value after a team key is its tag; anything else up to the next key is ignored.
                        if (team == 1) tag1 = s; else tag2 = s;
                        team = 0;
                    }
                    break;
                case (byte)'q' or (byte)'h':
                    i += 1;
                    break;
                case (byte)'r' or (byte)'j':
                    i += 4;
                    break;
                case (byte)'}' or (byte)'(' or (byte)'s' or (byte)'u':
                    break;
                default:
                    return null;
            }
        }
    }

    private static bool Valid(ReadOnlySpan<byte> tag) => !tag.IsEmpty && !tag.ContainsAnyExcept(TagChars);

    [Conditional("DEBUG")]
    internal static void SelfCheck()
    {
        static (string, string)? P(byte[] b) => Parse(b);
        static byte[] B(string s) => Encoding.Latin1.GetBytes(s);

        Debug.Assert(P(B("U\u0009opponents}q\u0008(U\u00011}q\tU\u0004nameq\nU\u0004COMAsU\u00012}q\u000bh\nU\u0005NED-1suu.")) == ("COMA", "NED-1"), "1.x slice");
        Debug.Assert(P(B("U\u0009opponentsq\u001e}q\u001f(U\u00011}q U\u0004nameq!U\u0005NED-1sU\u00012}q\"h!U\u0005KRAWLsuu.")) == ("NED-1", "KRAWL"), "2.x skirmish slice");
        Debug.Assert(P(B("U\u0009opponents}q\u0008(U\u00011}q\tU\u0004nameq\nU\u0005NED-1sU\u00012}q\u000bh\nU\u0005ZSYUAsuu.")) == ("NED-1", "ZSYUA"), "temp3 slice");
        Debug.Assert(P(B("U\u0009opponents}q\u0008(U\u00011}q\tU\u0004nameq\nU\u0004MSSZsU\u00012}q\u000bh\nU\u0005NED-1suu.")) == ("MSSZ", "NED-1"), "temp4 slice");
        Debug.Assert(P(B("U\u0009opponents}q\u0008(U\u00011}q\tU\u0004nameq\nU\u0005NED-1sU\u00012}q\u000bh\nU\u0005UTD-Usuu.")) == ("NED-1", "UTD-U"), "temp5 slice");
        Debug.Assert(P(B("random battle, no block")) is null, "no opponents");
        Debug.Assert(P(B("U\u0009opponents}q\u0008(U\u00011}q\tU\u0004nameq\nU\u0004COMA")) is null, "truncated");
        Debug.Assert(P([]) is null, "empty");
        Debug.Assert(P(B("U\u0009opponents}(U\u00011}U\u0004nameU\u0000sU\u00012}U\u0004nameU\u0002ABsuu.")) is null, "empty tag");
        Debug.Assert(P(B("U\u0009opponents}(U\u00011}U\u0004nameU\u0000U\u0001XsU\u00012}U\u0004nameU\u0002ABsuu.")) is null, "empty tag, later value ignored");
    }
}
