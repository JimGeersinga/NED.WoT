namespace NED.WoT.BattleResults.Client.Services
{
    public static class TankNameResolver
    {
        public static HashSet<string> UndefinedTankNames = new();

        private static IReadOnlyDictionary<string, string> _tankNames = new Dictionary<string, string>()
        {
            {"uk:GB98_T95_FV4201_Chieftain", "T95/FV4201" },
            {"ussr:R149_Object_268_4", "Object 268/4" },
            {"uk:GB91_Super_Conqueror", "Super Conqueror" },
            {"usa:A85_T110E3", "T110E3" },
            {"ussr:R110_Object_260", "Object 260" },
            {"france:F10_AMX_50B", "AMX 50B" },
            {"ussr:R157_Object_279R", "Object 279 (e)" },
            {"ussr:R155_Object_277", "Object 277" },
            {"germany:G42_Maus", "Maus" },
            {"ussr:R97_Object_140", "Object 140" },
            {"poland:Pl21_CS_63", "CS-63" },
            {"germany:G56_E-100", "E 100" },
            {"ussr:R95_Object_907", "Object 907" },
            {"poland:Pl15_60TP_Lewandowskiego", "60TP" },
            {"france:F82_AMX_M4_Mle1949_Ter", "AMX M4 54" },
            {"japan:J20_Type_2605", "Type 5 Heavy" },
            {"usa:A83_T110E4", "T110E4" },
            {"czech:Cz17_Vz_55", "Vz-55" },
            {"ussr:R45_IS-7", "IS-7" },
            {"usa:A69_T110E5", "T110E5" },
            {"ussr:R132_VNII_100LT", "T-100 LT" },
            {"uk:GB100_Manticore", "Manticore" },
            {"germany:G92_VK7201", "VK 27.01 (K)" },
            {"france:F18_Bat_Chatillon25t", "Bat.-Chatillon 25 t" },
            {"ussr:R90_IS_4M", "IS-4" },
            {"germany:G72_JagdPz_E100", "Jagdpanzer E100" },
            {"china:Ch41_WZ_111_5A", "WZ-111 5A" },
        };

        public static string GetTankName(string key)
        {
            if (_tankNames.TryGetValue(key, out var name))
            {
                return name;
            }

            UndefinedTankNames.Add(key);

            return key;
        }
    }
}



