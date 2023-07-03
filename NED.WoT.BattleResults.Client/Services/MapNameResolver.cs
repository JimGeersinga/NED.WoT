namespace NED.WoT.BattleResults.Client.Services
{
    public static class MapNameResolver
    {
        public static HashSet<string> UndefinedMapNames = new();

        private static readonly IReadOnlyDictionary<string, string> _mapNames = new Dictionary<string, string>()
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
            {"Stadspark","Widepark"}
        };

        public static string GetMapName(string key)
        {
            string strippedKey = key[(key.IndexOf(":") + 1)..];
            if (_mapNames.TryGetValue(strippedKey, out var name))
            {
                return name;
            }

            UndefinedMapNames.Add(key);

            return key;
        }
    }
}



