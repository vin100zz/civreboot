// Custom map format for "New Game" — a human-editable alternative to the original
// game's MAP.PIC (an indexed-palette bitmap, see MapManagement.LoadEarthMap/LoadCustomMap).
// A map is a JSON file under server/Maps/*.json:
//
//   {
//     "name": "Pangaea",
//     "rows": [
//       "WWWWWWWWWW...",   // 80 characters wide
//       "WWGGGGFFHHW...",
//       ...                 // 50 rows tall
//     ],
//     "startPositions": {
//       "Roman": [36, 19],
//       "Egyptian": [41, 24]
//     }
//   }
//
// Each character is one map tile (row 0 = top/north, column 0 = west edge):
//   D=Desert P=Plains G=Grassland F=Forest H=Hills M=Mountains
//   T=Tundra A=Arctic S=Swamp     J=Jungle W=Water  R=River
//
// Rows shorter than 80 characters are padded with Water; extra characters beyond
// 80 are ignored. Fewer than 50 rows are padded with all-Water rows.
//
// startPositions is optional and per-nationality (keys match NationDefinition's
// "Nationality" adjective — see GameData.cs's nationTypes list: Roman, Babylonian,
// German, Egyptian, American, Greek, Indian, Russian, Zulu, French, Aztec, Chinese,
// English, Mongol). This mirrors the original game's Array_35da, which hardcodes
// each nation's real-world start tile for "EARTH" games (StartGameMenu.cs ~line 543).
// Any nationality NOT listed just uses the normal random site-search — you don't have
// to specify all of them.
using System.Text.Json;

namespace OpenCivOne.Server.Maps
{
    // Grid: terrain per [x, y] cell. StartPositions: nationality name (case-insensitive)
    // -> start tile, for nations explicitly placed by the map; others use random search.
    public record CustomMapData(TerrainTypeEnum[,] Grid, IReadOnlyDictionary<string, (int X, int Y)> StartPositions);

    public static class CustomMapFormat
    {
        public const int Width = 80;
        public const int Height = 50;

        private static readonly Dictionary<char, TerrainTypeEnum> CharToTerrain = new()
        {
            ['D'] = TerrainTypeEnum.Desert,
            ['P'] = TerrainTypeEnum.Plains,
            ['G'] = TerrainTypeEnum.Grassland,
            ['F'] = TerrainTypeEnum.Forest,
            ['H'] = TerrainTypeEnum.Hills,
            ['M'] = TerrainTypeEnum.Mountains,
            ['T'] = TerrainTypeEnum.Tundra,
            ['A'] = TerrainTypeEnum.Arctic,
            ['S'] = TerrainTypeEnum.Swamp,
            ['J'] = TerrainTypeEnum.Jungle,
            ['W'] = TerrainTypeEnum.Water,
            ['R'] = TerrainTypeEnum.River,
        };

        private static readonly Dictionary<TerrainTypeEnum, char> TerrainToChar =
            CharToTerrain.ToDictionary(kv => kv.Value, kv => kv.Key);

        // server/Maps, resolved relative to the build output (bin/Debug/netX/) so it
        // works the same whether run via `dotnet run` or a built binary.
        private static string MapsDir() => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Maps");

        // Lists available map names (JSON file names under Maps/, without extension),
        // for the New Game dropdown.
        public static string[] ListMaps()
        {
            string dir = MapsDir();
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)
                .Select(n => n!)
                .OrderBy(n => n)
                .ToArray();
        }

        // Loads a map by name: terrain grid as [x, y] (matching MapManagement's
        // GetTerrainType/SetTerrainType (x, y) convention) plus any per-nationality
        // start positions the map defines.
        public static CustomMapData Load(string mapName)
        {
            string path = Path.Combine(MapsDir(), mapName + ".json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"Custom map '{mapName}' not found at {path}");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var rowsElement = doc.RootElement.GetProperty("rows");
            var rows = rowsElement.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();

            var grid = new TerrainTypeEnum[Width, Height];
            for (int y = 0; y < Height; y++)
            {
                string row = y < rows.Length ? rows[y] : "";
                for (int x = 0; x < Width; x++)
                {
                    char c = x < row.Length ? char.ToUpperInvariant(row[x]) : 'W';
                    grid[x, y] = CharToTerrain.TryGetValue(c, out var t) ? t : TerrainTypeEnum.Water;
                }
            }

            var startPositions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("startPositions", out var sp) && sp.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in sp.EnumerateObject())
                {
                    var coords = prop.Value.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                    if (coords.Length == 2)
                        startPositions[prop.Name] = (coords[0], coords[1]);
                }
            }

            return new CustomMapData(grid, startPositions);
        }

        // Converts a live terrain grid back to row strings, for saving/exporting a map
        // (e.g. from MAP.PIC, or a future in-browser editor) in the same JSON format.
        public static string[] ToRows(Func<int, int, TerrainTypeEnum> getTerrainType)
        {
            var rows = new string[Height];
            for (int y = 0; y < Height; y++)
            {
                var chars = new char[Width];
                for (int x = 0; x < Width; x++)
                {
                    var t = getTerrainType(x, y);
                    chars[x] = TerrainToChar.TryGetValue(t, out var c) ? c : 'W';
                }
                rows[y] = new string(chars);
            }
            return rows;
        }
    }
}
