namespace NED.WoT.BattleResults.Client.Services;

public static class MapNameResolver
{
    public static readonly HashSet<string> UndefinedMapNames = [];

    private static readonly Dictionary<string, string> _mapNames = new()
    {
        {"01_karelia/name", "Karelia"},
        {"Abbey", "Abbey"},
        {"Abdij","Abbey"},
        {"Airfield", "Airfield"},
        {"Arzagir 4.04", "Arzagir 4.04"},
        {"Babylon Anomaly", "Babylon Anomaly"},
        {"Babylon Facility", "Babylon Facility"},
        {"Bergpas","Mountain Pass"},
        {"Berlin", "Berlin"},
        {"Brawl School", "Brawl School"},
        {"Buitenpost","Outpost"},
        {"Cliff", "Cliff"},
        {"Dreamland", "Dreamland"},
        {"EBR Race - Karelia", "EBR Race - Karelia"},
        {"El Halluf", "El Halluf"},
        {"Empire's Border", "Empire's Border"},
        {"Ensk", "Ensk"},
        {"Ensk Region", "Ensk Region"},
        {"Erlenberg", "Erlenberg"},
        {"Fallenstadt", "Fallenstadt"},
        {"Fata Morgana", "Fata Morgana"},
        {"Firnulfir", "Firnulfir"},
        {"Fisherman's Bay", "Fisherman's Bay"},
        {"Fjorden","Fjords"},
        {"Fjords", "Fjords"},
        {"Ghost Town", "Ghost Town"},
        {"Glacier", "Glacier"},
        {"Glacier: Night", "Glacier: Night"},
        {"Gletsjer","Glacier"},
        {"Highway", "Highway"},
        {"Himmelsdorf", "Himmelsdorf"},
        {"Hinterland", "Hinterland"},
        {"Karelia", "Karelia"},
        {"Karelia (Winter)", "Karelia (Winter)"},
        {"Karelië","Karelia"},
        {"Klif","Cliff"},
        {"Klondike", "Klondike"},
        {"Kraftwerk", "Kraftwerk"},
        {"Lakeville", "Lakeville"},
        {"Lauerberg", "Lauerberg"},
        {"Live Oaks", "Live Oaks"},
        {"Malinovka", "Malinovka"},
        {"Malinovka (Winter)", "Malinovka (Winter)"},
        {"Mannerheim Line", "Mannerheim Line"},
        {"Mannerheim Linie","Mannerheim Line"},
        {"Mijngebied","Mines"},
        {"Mines", "Mines"},
        {"Mirror Phase", "Mirror Phase"},
        {"Mountain Pass", "Mountain Pass"},
        {"Murovanka", "Murovanka"},
        {"Nebelburg", "Nebelburg"},
        {"New School", "New School"},
        {"Nordskar", "Nordskar"},
        {"Normandie", "Normandie"},
        {"Oder", "Oder"},
        {"Oesterbaai","Oyster Bay"},
        {"Old School", "Old School"},
        {"Omaha Beach", "Omaha Beach"},
        {"Outpost", "Outpost"},
        {"Outpost: Night", "Outpost: Night"},
        {"Overlord", "Overlord"},
        {"Oyster Bay", "Oyster Bay"},
        {"Parelrivier","Pearl River"},
        {"Parijs","Paris"},
        {"Paris", "Paris"},
        {"Pearl River", "Pearl River"},
        {"Phase 137", "Phase 137"},
        {"Pilsen", "Pilsen"},
        {"Prokhorovka", "Prokhorovka"},
        {"Prokhorovka (Winter)", "Prokhorovka (Winter)"},
        {"Province", "Province"},
        {"Provincie","Province"},
        {"Redshire", "Redshire"},
        {"Rijksgrens","Empire's Border"},
        {"Ruinberg", "Ruinberg"},
        {"Safe Haven", "Safe Haven"},
        {"Sand River", "Sand River"},
        {"Sand River: Night", "Sand River: Night"},
        {"Serene Coast", "Serene Coast"},
        {"Serene kust","Serene Coast"},
        {"Siegfried Line", "Siegfried Line"},
        {"Siegfriedlinie","Siegfried Line"},
        {"Snelweg","Highway"},
        {"Spookstad","Ghost Town"},
        {"Stadspark","Widepark"},
        {"Steppe","Steppes"},
        {"Steppes", "Steppes"},
        {"Steppes: Night", "Steppes: Night"},
        {"Studzianki", "Studzianki"},
        {"Toendra","Tundra"},
        {"Tundra", "Tundra"},
        {"Veilige haven","Safe Haven"},
        {"Vissersbaai","Fisherman's Bay"},
        {"Vliegveld","Airfield"},
        {"Westfield", "Westfield"},
        {"Westwall", "Westwall"},
        {"Widepark", "Widepark"},
        {"Zandrivier","Sand River"}
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