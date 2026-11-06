using System.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace NED.WoT.BattleResults.Client.Models;

/// <summary>The lookup behind the Clan and Speler dropdowns of the filter bar: every clan and every player in the readable battles, built once (lazily, on the first search) and thrown away when the battles change. The markup stays thin; this is the logic (as <see cref="BattleStats"/> is for the stats).</summary>
public sealed class BattleFilters
{
    /// <summary>One line of a dropdown. A clan: Name is its tag, Clan the same tag, Battles the battles it is in. A player: Name, Clan the tag of their latest battle (null when they had none) and, with a clan chosen, Battles the battles with that clan. A section header: only Header is set.</summary>
    public sealed record Option(string Name, string? Clan, int Battles, string? Header = null);

    /// <summary>The icons of the two boxes: the game-art glyphs of the design board, as markup like the MudBlazor icons.</summary>
    public const string Shield = "<path fill-rule=\"evenodd\" d=\"M12 2.5l7.5 2.8v6.2c0 4.6-3.1 8.6-7.5 10.5-4.4-1.9-7.5-5.9-7.5-10.5V5.3zM12 7.4L8 9.9v1.3l4-2.2 4 2.2V9.9zM12 12.2l-4 2.5V16l4-2.2 4 2.2v-1.3z\"/>";
    public const string Helmet = "<path fill-rule=\"evenodd\" d=\"M12 3C7.6 3 4.5 6.4 4.5 10.8V15l1.5 1.5V19h12v-2.5l1.5-1.5v-4.2C19.5 6.4 16.4 3 12 3zM6.8 10.4h10.4v3.2H6.8z\"/>";

    private readonly IReadOnlyCollection<BattleReport> _reports;
    private readonly Lazy<Tables> _tables;

    private BattleFilters(IReadOnlyCollection<BattleReport> reports, string ownTag)
    {
        _reports = reports;
        OwnTag = ownTag;
        _tables = new Lazy<Tables>(() => Tables.From(_reports, OwnTag));
    }

    /// <summary>Your own clan tag, as it was when the lookup was made: a changed setting needs a new lookup.</summary>
    public string OwnTag { get; }

    /// <summary>The lookup of a set of battles. Nothing is read until the first search.</summary>
    public static BattleFilters Build(IReadOnlyCollection<BattleReport> reports, string? ownTag) => new(reports, ownTag ?? string.Empty);

    private sealed record Tables(Option[] Clans, Option[] Players, int OwnPlayers, Dictionary<string, Option[]> ByClan)
    {
        public static Tables From(IEnumerable<BattleReport> reports, string ownTag)
        {
            Dictionary<string, int> clanBattles = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, (string? Clan, DateTime At)> latest = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Dictionary<string, int>> perClan = new(StringComparer.OrdinalIgnoreCase);

            foreach (BattleReport report in reports)
            {
                HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);

                foreach (Team team in new[] { report.Team1, report.Team2 })
                {
                    string? tag = IsTag(team.Abbreviation) ? team.Abbreviation : null;
                    if (tag != null)
                    {
                        tags.Add(tag);
                    }

                    foreach (Player player in team.Players.Where(x => !string.IsNullOrEmpty(x.Name)).DistinctBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        string name = player.Name!;
                        if (!latest.TryGetValue(name, out var known) || report.MatchStart >= known.At)
                        {
                            latest[name] = (string.IsNullOrEmpty(player.Clan) ? null : player.Clan, report.MatchStart);
                        }

                        if (tag != null)
                        {
                            if (!perClan.TryGetValue(tag, out var names))
                            {
                                perClan[tag] = names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            }

                            names[name] = names.GetValueOrDefault(name) + 1;
                        }
                    }
                }

                foreach (string tag in tags)
                {
                    clanBattles[tag] = clanBattles.GetValueOrDefault(tag) + 1;
                }
            }

            Option[] clans = clanBattles
                .Select(x => new Option(x.Key, x.Key, x.Value))
                .OrderByDescending(x => x.Battles)
                .ThenBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();

            // Your own clan's players first, each group A to Z. The tag is the one of the latest battle.
            Option[] all = latest.Select(x => new Option(x.Key, x.Value.Clan, 0)).ToArray();
            Option[] own = all.Where(x => SameTag(x.Clan, ownTag)).OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase).ToArray();
            Option[] rest = all.Where(x => !SameTag(x.Clan, ownTag)).OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase).ToArray();

            Dictionary<string, Option[]> byClan = perClan.ToDictionary(
                x => x.Key,
                x => x.Value.Select(p => new Option(p.Key, latest[p.Key].Clan, p.Value)).OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);

            return new Tables(clans, [.. own, .. rest], own.Length, byClan);
        }
    }

    public static bool SameTag(string? tag, string? other) => !string.IsNullOrWhiteSpace(tag) && string.Equals(tag, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>A real clan tag: a normal battle has none and its teams are "?".</summary>
    private static bool IsTag(string? tag) => !string.IsNullOrEmpty(tag) && tag != "?";

    private static bool Has(string text, string term) => text.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static Option Header(string text) => new(string.Empty, null, 0, text);

    /// <summary>The Clan list for the typed term: your own clan (with its battle count, 0 when it has none), then the opponents, most battles first.</summary>
    public IEnumerable<Option> ClanOptions(string? term)
    {
        term ??= string.Empty;
        Tables tables = _tables.Value;
        Option? mine = OwnTag.Length == 0 ? null : tables.Clans.FirstOrDefault(x => SameTag(x.Name, OwnTag)) ?? new Option(OwnTag, OwnTag, 0);
        List<Option> others = tables.Clans.Where(x => !SameTag(x.Name, OwnTag) && Has(x.Name, term)).ToList();
        List<Option> rows = [];

        if (mine != null && Has(mine.Name, term))
        {
            rows.Add(Header("Jouw clan"));
            rows.Add(mine);
        }

        if (others.Count > 0)
        {
            rows.Add(Header($"Tegenstanders · {others.Count}"));
            rows.AddRange(others);
        }

        return rows;
    }

    /// <summary>The clan tag in front of the Speler field, only while a player is picked: the chosen clan's, otherwise that player's latest clan. Null (the field shows only the helmet icon) without a player, also with a clan chosen, and for a player with no clan tag and no clan chosen.</summary>
    public static string? SpelerTag(string? chosenClan, Option? player) => player == null ? null : chosenClan ?? (string.IsNullOrEmpty(player.Clan) ? null : player.Clan);

    /// <summary>The Speler list for the typed term. With a clan: only the players who were ever on that clan's team, each with the battles with that clan. Without: every player once, your own clan's players first, then the others, each A to Z.</summary>
    public IEnumerable<Option> PlayerOptions(string? clan, string? term)
    {
        term ??= string.Empty;
        Tables tables = _tables.Value;
        List<Option> rows = [];

        if (clan != null)
        {
            Option[] players = tables.ByClan.GetValueOrDefault(clan) ?? [];
            IEnumerable<Option> hits = players.Where(x => Has(x.Name, term));
            AddSection(rows, $"Spelers van [{clan}]", hits);
            return rows;
        }

        Option[] everyone = tables.Players;
        AddSection(rows, $"{OwnTag} · A tot Z", OwnTag.Length == 0 ? [] : everyone.Take(tables.OwnPlayers).Where(x => Has(x.Name, term)));
        AddSection(rows, "Overige spelers · A tot Z", everyone.Skip(tables.OwnPlayers).Where(x => Has(x.Name, term)));
        return rows;
    }

    private static void AddSection(List<Option> rows, string header, IEnumerable<Option> options)
    {
        int at = rows.Count;
        rows.Add(Header(header));
        rows.AddRange(options);

        if (rows.Count == at + 1)
        {
            rows.RemoveAt(at);
        }
    }

    /// <summary>The player to keep after a clan is chosen: the same player when they were ever on that clan's team (so they are in the Speler list for it), otherwise null.</summary>
    public string? PlayerAfterClan(string player, string clan) =>
        (_tables.Value.ByClan.GetValueOrDefault(clan) ?? []).Any(x => string.Equals(x.Name, player, StringComparison.OrdinalIgnoreCase)) ? player : null;

    /// <summary>The text with every occurrence of the typed term in bold (case-insensitive); Razor encodes the text.</summary>
    public static RenderFragment Highlighted(string text, string? term) => builder =>
    {
        int at = 0;
        int hit = string.IsNullOrEmpty(term) ? -1 : text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        while (hit >= 0)
        {
            builder.AddContent(0, text[at..hit]);
            builder.OpenElement(1, "b");
            builder.AddContent(2, text.Substring(hit, term!.Length));
            builder.CloseElement();
            at = hit + term.Length;
            hit = text.IndexOf(term, at, StringComparison.OrdinalIgnoreCase);
        }

        builder.AddContent(3, text[at..]);
    };

    [Conditional("DEBUG")]
    public static void SelfCheck()
    {
        DateTime day = new(2026, 3, 1, 20, 0, 0);
        BattleReport Battle(int hours, string own, string other, string[] ownPlayers, string[] otherPlayers)
        {
            BattleReport report = new() { MatchStart = day.AddHours(hours) };
            report.Team1 = new Team { Number = 1, Abbreviation = own };
            report.Team2 = new Team { Number = 2, Abbreviation = other };
            report.Team1.Players = ownPlayers.Select(x => new Player(report.Team1) { Name = x, Clan = own }).ToList();
            report.Team2.Players = otherPlayers.Select(x => new Player(report.Team2) { Name = x, Clan = other }).ToList();
            return report;
        }

        List<BattleReport> reports =
        [
            Battle(1, "NED-1", "DUTCH", ["Tankgeneraal", "Klompenklopper"], ["KlompKees", "KaasKoning"]),
            Battle(2, "NED-1", "DUTCH", ["Tankgeneraal"], ["KlompKees"]),
            Battle(3, "NED-1", "DUTCH", ["Dijkdoorbraak"], ["KlompKees"]),
            Battle(4, "NED-1", "RHINE", ["Tankgeneraal"], ["Kapitein_Kees"]),
            Battle(5, "?", "?", ["Zonder"], ["Clan"])
        ];
        BattleFilters filters = Build(reports, "NED-1");

        List<Option> clans = filters.ClanOptions(string.Empty).Where(x => x.Header == null).ToList();
        Debug.Assert(clans.Select(x => x.Name).SequenceEqual(["NED-1", "DUTCH", "RHINE"]) && clans.Select(x => x.Battles).SequenceEqual([4, 3, 1]), "clans: yours first, then most battles first; a normal battle is no clan");
        Debug.Assert(filters.ClanOptions(string.Empty).Select(x => x.Header).OfType<string>().SequenceEqual(["Jouw clan", "Tegenstanders · 2"]), "clan headers: yours, the opponents with their number");
        Debug.Assert(filters.ClanOptions("rh").Where(x => x.Header == null).Select(x => x.Name).SequenceEqual(["RHINE"]), "a typed term narrows the clans, case does not matter");

        // No clan, typed "k": NED-1 players first, A to Z, then the others A to Z.
        List<Option> typed = filters.PlayerOptions(null, "k").ToList();
        Debug.Assert(typed.Where(x => x.Header == null).Select(x => x.Name).SequenceEqual(["Dijkdoorbraak", "Klompenklopper", "Tankgeneraal", "KaasKoning", "Kapitein_Kees", "KlompKees"]), "players without a clan: own clan first, then the others, A to Z");
        Debug.Assert(typed.Select(x => x.Header).OfType<string>().SequenceEqual(["NED-1 · A tot Z", "Overige spelers · A tot Z"]), "the two sections have their header");
        Debug.Assert(filters.PlayerOptions(null, string.Empty).Count(x => x.Header == null) == 8, "players without a clan: every player seen in the battles, once");
        Debug.Assert(filters.PlayerOptions(null, string.Empty).Single(x => x.Name == "KlompKees").Clan == "DUTCH", "a player carries the clan of the latest battle");

        // A clan: only its players, with the battles with that clan.
        List<Option> dutch = filters.PlayerOptions("DUTCH", string.Empty).Where(x => x.Header == null).ToList();
        Debug.Assert(dutch.Select(x => x.Name).SequenceEqual(["KaasKoning", "KlompKees"]) && dutch.Select(x => x.Battles).SequenceEqual([1, 3]), "players of a clan: only that clan's, with battle counts");

        // The tag of the Speler field, only with a player picked: the chosen clan, else the player's latest clan; none at all without either.
        Option picked = new("KlompKees", "DUTCH", 0);
        Debug.Assert(SpelerTag(null, null) == null && SpelerTag("RHINE", null) == null, "Speler tag: no player, no tag, also with a clan chosen");
        Debug.Assert(SpelerTag(null, picked) == "DUTCH" && SpelerTag("RHINE", picked) == "RHINE" && SpelerTag(null, new Option("Zonder", null, 0)) == null && SpelerTag("RHINE", new Option("Zonder", null, 0)) == "RHINE", "Speler tag: the chosen clan, else the player's clan, else no tag (helmet icon only)");

        // Choosing a clan keeps a player who was ever on it and drops one who never was.
        Debug.Assert(filters.PlayerAfterClan("KlompKees", "DUTCH") == "KlompKees" && filters.PlayerAfterClan("klompkees", "RHINE") == null && filters.PlayerAfterClan("Kapitein_Kees", "RHINE") == "Kapitein_Kees", "a chosen player stays only when they were on the chosen clan");

        // A player of two clans is one row without a clan and a row under each clan.
        List<BattleReport> moved = [Battle(1, "NED-1", "DUTCH", ["Tankgeneraal"], ["Wisselaar"]), Battle(2, "NED-1", "RHINE", ["Tankgeneraal"], ["Wisselaar"])];
        BattleFilters change = Build(moved, "NED-1");
        Debug.Assert(change.PlayerOptions(null, "wissel").Count(x => x.Header == null) == 1 && change.PlayerOptions(null, "wissel").Single(x => x.Header == null).Clan == "RHINE", "one row, with the tag of the latest battle");
        Debug.Assert(change.PlayerOptions("DUTCH", "wissel").Count(x => x.Header == null) == 1 && change.PlayerOptions("RHINE", "wissel").Count(x => x.Header == null) == 1, "listed under every clan they were ever on");
    }
}
