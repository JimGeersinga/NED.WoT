namespace NED.WoT.BattleResults.Client.Services;

public static class MapNameResolver
{
    public static readonly HashSet<string> UndefinedMapNames = [];

    private static readonly Dictionary<string, string> _mapNames = new()
    {
        {"Abdij","Abbey"},
        {"Vliegveld","Airfield"},
        {"Klif","Cliff"},
        {"Rijksgrens","Empire's Border"},
        {"Vissersbaai","Fisherman's Bay"},
        {"Fjorden","Fjords"},
        {"Spookstad","Ghost Town"},
        {"Gletsjer","Glacier"},
        {"Snelweg","Highway"},
        {"Karelië","Karelia"},
        {"Mannerheim Linie","Mannerheim Line"},
        {"Mijngebied","Mines"},
        {"Bergpas","Mountain Pass"},
        {"Buitenpost","Outpost"},
        {"Oesterbaai","Oyster Bay"},
        {"Parelrivier","Pearl River"},
        {"Parijs","Paris"},
        {"Provincie","Province"},
        {"Veilige haven","Safe Haven"},
        {"Zandrivier","Sand River"},
        {"Serene kust","Serene Coast"},
        {"Siegfriedlinie","Siegfried Line"},
        {"Steppe","Steppes"},
        {"Toendra","Tundra"},
        {"Stadspark","Widepark"},

        {"Karelia", "Karelia"},
        {"Lakeville", "Lakeville"},
        {"Ghost Town", "Ghost Town"},
        {"Sand River", "Sand River"},
        {"El Halluf", "El Halluf"},
        {"Malinovka", "Malinovka"},
        {"Himmelsdorf", "Himmelsdorf"},
        {"Paris", "Paris"},
        {"Siegfried Line", "Siegfried Line"},
        {"Ruinberg", "Ruinberg"},
        {"Steppes", "Steppes"},
        {"Prokhorovka", "Prokhorovka"},
        {"Province", "Province"},
        {"Mines", "Mines"},
        {"Outpost", "Outpost"},
        {"Cliff", "Cliff"},
        {"Abbey", "Abbey"},
        {"Pearl River", "Pearl River"},
        {"Berlin", "Berlin"},
        {"Pilsen", "Pilsen"},
        {"Tundra", "Tundra"},
        {"Live Oaks", "Live Oaks"},
        {"Mannerheim Line", "Mannerheim Line"},
        {"Fisherman's Bay", "Fisherman's Bay"},
        {"Mountain Pass", "Mountain Pass"},
        {"Widepark", "Widepark"},
        {"Glacier", "Glacier"},
        {"Murovanka", "Murovanka"},
        {"Erlenberg", "Erlenberg"},
        {"Redshire", "Redshire"},
        {"Westfield", "Westfield"},
        {"Overlord", "Overlord"},
        {"Fjords", "Fjords"},
        {"Ensk", "Ensk"},
        {"Airfield", "Airfield"},
        {"Highway", "Highway"},
        {"Safe Haven", "Safe Haven"},
        {"Empire's Border", "Empire's Border"}
    };

    public static string? GetMapName(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return key;
        }

        if (_mapNames.TryGetValue(key, out string? name))
        {
            return name;
        }

        ReadOnlySpan<char> keySpan = key.AsSpan();

        Dictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> lookup = _mapNames.GetAlternateLookup<ReadOnlySpan<char>>();

        int semiColonIndex = keySpan.IndexOf(':');
        if (semiColonIndex != -1)
        {
            ReadOnlySpan<char> strippedKey = keySpan[(semiColonIndex + 1)..];
            if (lookup.TryGetValue(strippedKey, out name))
            {
                return name;
            }
        }

        UndefinedMapNames.Add(key);

        return key;
    }
}