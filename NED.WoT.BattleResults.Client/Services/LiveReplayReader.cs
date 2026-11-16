using System.Diagnostics;
using System.Text;

namespace NED.WoT.BattleResults.Client.Services;

/// <summary>Reads the two clan tags from a live temp.wotreplay. That file has no JSON header (it is the raw packet stream); the tags sit in a pickled <c>opponents</c> dict near the start. Never unpickles: the file is untrusted input.</summary>
public static class LiveReplayReader
{
    private const int MaxRead = 64 * 1024;
    private const int MaxWalk = 256;

    /// <returns>The clan tags of team 1 and team 2, or null while the block is not there (yet) or is not the known shape.</returns>
    public static (string, string)? ReadClans(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] buffer = new byte[MaxRead];
        int read = 0;
        int n;
        while (read < buffer.Length && (n = stream.Read(buffer, read, buffer.Length - read)) > 0)
        {
            read += n;
        }
        return Parse(buffer.AsSpan(0, read));
    }

    internal static (string, string)? Parse(ReadOnlySpan<byte> data)
    {
        int key = data.IndexOf("opponents"u8);
        if (key < 0)
        {
            return null;
        }

        ReadOnlySpan<byte> p = data[(key + 9)..];
        string?[] tags = new string?[3];
        int team = 0;
        int i = 0;
        while (true)
        {
            if (i >= p.Length || i >= MaxWalk)
            {
                return null;
            }

            byte op = p[i++];
            switch (op)
            {
                case (byte)'.':
                    return Valid(tags[1]) && Valid(tags[2]) ? (tags[1]!, tags[2]!) : null;
                case (byte)'U':
                    if (i >= p.Length || i + 1 + p[i] > p.Length)
                    {
                        return null;
                    }
                    int len = p[i];
                    string s = Encoding.ASCII.GetString(p.Slice(i + 1, len));
                    i += 1 + len;
                    if (s == "1") team = 1;
                    else if (s == "2") team = 2;
                    else if (s != "name" && team > 0 && tags[team] is null) tags[team] = s;
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

    private static bool Valid(string? tag) =>
        !string.IsNullOrEmpty(tag) && tag.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

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
    }
}
