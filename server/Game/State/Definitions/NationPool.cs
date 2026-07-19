using System.Text.Json;

namespace OpenCivOne
{
    // Configurable pool of playable civilizations, loaded from server/Data/Nations.json.
    // Each entry looks like:
    //
    //   {
    //     "leader": "Caesar", "nation": "Romans", "nationality": "Roman",
    //     "mood": 0,     // -1 = Friendly, 0 = Neutral, 1 = Aggressive
    //     "policy": 1,   // -1 = Perfectionist, 0 = Neutral, 1 = Expansionistic
    //     "ideology": 1, // -1 = Militaristic, 0 = Neutral, 1 = Civilized
    //     "shortTune": 24, "longTune": 10,
    //     "color": "#FFAD14",  // hex — sent to the client in /api/state and used for
    //                          // units/borders/UI (see StateSerializer.BuildPlayers and
    //                          // client/js/renderer.js). Colors MAY repeat across the
    //                          // pool — the original game reuses each of its 7 colors
    //                          // for two civilizations (e.g. Roman/Russian both yellow),
    //                          // because nation IDs 1-7 and 9-15 are two "flavors" of
    //                          // the SAME 7 player slots, not 14 independent slots: each
    //                          // AI opponent independently rolls between its slot's two
    //                          // flavors at game start (StartGameMenu.cs ~line 464), so
    //                          // both flavors of a slot must share that slot's color for
    //                          // the slot's color to stay stable regardless of the roll.
    //                          // See BuildNations for how the pool's colors get mapped
    //                          // onto slots so that no two of the 7 *slots* ever share
    //                          // a color.
    //     "cities": ["Rome", "Caesarea", ...]  // 16 names
    //   }
    //
    // The file may define any number of civilizations, as long as there are at least
    // GroupSize distinct colors, each used by at least one civilization. IDs 0 and 8 are
    // fixed "barbarian" factions, not part of the pool — see BuildNations.
    public class NationPoolEntry
    {
        public string Leader { get; set; } = "";
        public string Nation { get; set; } = "";
        public string Nationality { get; set; } = "";
        public short Mood { get; set; }
        public short Policy { get; set; }
        public short Ideology { get; set; }
        public short ShortTune { get; set; }
        public short LongTune { get; set; }
        public string[] Cities { get; set; } = Array.Empty<string>();
        public string Color { get; set; } = "#FF0200";
    }

    public static class NationPool
    {
        // 7 player slots, each with 2 nation "flavors" (nation IDs 1-7 and 9-15).
        public const int GroupSize = 7;

        private static List<NationPoolEntry>? cachedPool;

        // server/Data, resolved relative to the build output, mirroring CustomMapFormat.MapsDir().
        private static string DataDir() => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data");

        public static List<NationPoolEntry> LoadPool()
        {
            if (cachedPool != null) return cachedPool;

            string path = Path.Combine(DataDir(), "Nations.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Nations pool file not found at {path}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var pool = JsonSerializer.Deserialize<List<NationPoolEntry>>(File.ReadAllText(path), options)
                ?? new List<NationPoolEntry>();

            int distinctColors = pool.Select(p => p.Color).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinctColors < GroupSize)
                throw new InvalidDataException(
                    $"Nations.json must define at least {GroupSize} distinct colors, one per player slot " +
                    $"(found {distinctColors} across {pool.Count} civilizations).");

            // GameData.RebuildCityNames packs every nation's Cities into a flat array
            // indexed by a byte (City.NameID), 16 slots per nation — a mismatched count
            // would misalign every nation's block, not just this one's.
            var badCityCount = pool.FirstOrDefault(p => p.Cities.Length != 16);
            if (badCityCount != null)
                throw new InvalidDataException(
                    $"Nations.json: '{badCityCount.Nationality}' has {badCityCount.Cities.Length} cities, must have exactly 16.");

            cachedPool = pool;
            return cachedPool;
        }

        // Picks GroupSize colors and, for each, a "flavor A" and "flavor B" civilization
        // sharing that color — one civ per slot per flavor. A slot's two flavors always
        // share a color because each AI opponent independently rolls between them at
        // game start (StartGameMenu.cs ~line 464): if they didn't share a color, that
        // roll could silently swap a slot's on-screen color mid-assignment, and two
        // different slots could end up showing the same color in the same game. Picking
        // GroupSize *different* colors for the GroupSize slots is what actually
        // guarantees two simultaneously active civs never share a color.
        //
        // If a color has only one civilization, that civilization is used for both
        // flavors of its slot (the slot's "other flavor" menu option just repeats it).
        // If a color has more than two, two are picked at random.
        private static (List<NationPoolEntry> FlavorA, List<NationPoolEntry> FlavorB) PickSlots(List<NationPoolEntry> pool, Random rng)
        {
            var colorGroups = pool
                .GroupBy(p => p.Color, StringComparer.OrdinalIgnoreCase)
                .OrderBy(_ => rng.Next())
                .Take(GroupSize)
                .ToList();

            var flavorA = new List<NationPoolEntry>();
            var flavorB = new List<NationPoolEntry>();
            foreach (var colorGroup in colorGroups)
            {
                var members = colorGroup.OrderBy(_ => rng.Next()).ToList();
                flavorA.Add(members[0]);
                flavorB.Add(members.Count > 1 ? members[1] : members[0]);
            }
            return (flavorA, flavorB);
        }

        // Builds the 16-slot runtime nation array: IDs 0 and 8 are fixed barbarian
        // factions, IDs 1-7 and 9-15 are the two flavors of each of the GroupSize player
        // slots (see PickSlots). Shuffling is deterministic from `seed`
        // (GameData.RandomSeed) so the same seed always reproduces the same draw.
        public static NationDefinition[] BuildNations(int seed)
        {
            var pool = LoadPool();
            var rng = new Random(seed);
            var (groupA, groupB) = PickSlots(pool, rng);

            var nations = new NationDefinition[16];
            nations[0] = new NationDefinition(0, "Attila", "Barbarians", "Barbarian", 0, 0, 0, 36, 36,
                new string[] { "Mecca", "Naples", "Sidon", "Tyre", "Tarsus", "Issus", "Cunaxa", "Cremona", "Cannae", "Capua",
                    "Turin", "Genoa", "Utica", "Crete", "Damascus", "Verona" }, "#404040");
            nations[8] = new NationDefinition(8, "", "", "", 0, 0, 0, 36, 36,
                new string[] { "Salamis", "Lisbon", "Hamburg", "Prague", "Salzburg", "Bergen", "Venice", "Milan", "Ghent", "Pisa",
                    "Cordoba", "Seville", "Dublin", "Toronto", "Melbourne", "Sydney" }, "#404040");

            for (int i = 0; i < GroupSize; i++)
            {
                var e = groupA[i];
                nations[1 + i] = new NationDefinition(1 + i, e.Leader, e.Nation, e.Nationality, e.Mood, e.Policy, e.Ideology, e.ShortTune, e.LongTune, e.Cities, e.Color);
            }
            for (int i = 0; i < GroupSize; i++)
            {
                var e = groupB[i];
                nations[9 + i] = new NationDefinition(9 + i, e.Leader, e.Nation, e.Nationality, e.Mood, e.Policy, e.Ideology, e.ShortTune, e.LongTune, e.Cities, e.Color);
            }

            return nations;
        }
    }
}
