// Phase 3 — Serializes OpenCivOne GameData to a JSON-friendly DTO.
// Called after each game state change to broadcast to the web client.
using System.Text.Json;

namespace OpenCivOne.Server
{
    public static class StateSerializer
    {
        public static string Serialize(OpenCivOneGame game, string pendingAction = "")
        {
            var gd = game.GameData;

            var tiles = BuildTileArray(game);
            var cities = BuildCities(gd);
            var units = BuildUnits(gd);
            var players = BuildPlayers(gd);

            var state = new
            {
                turn = gd.TurnCount,
                year = gd.Year,
                humanPlayerID = gd.HumanPlayerID,
                difficulty = gd.DifficultyLevel,
                aiOpponentCount = gd.AIOpponentCount,
                activeCivs = Convert.ToString(gd.ActiveCivilizations, 2).PadLeft(8, '0'),
                pendingAction,
                map = new
                {
                    width = 80,
                    height = 50,
                    tiles
                },
                cities,
                units,
                players,
            };

            return JsonSerializer.Serialize(state);
        }

        private static object[][] BuildTileArray(OpenCivOneGame game)
        {
            var gd = game.GameData;
            var mm = game.MapManagement;
            var tiles = new object[50][];
            for (int y = 0; y < 50; y++)
            {
                tiles[y] = new object[80];
                for (int x = 0; x < 80; x++)
                {
                    int terrain = (int)mm.GetTerrainType(x, y);
                    int improvements = (int)mm.F0_2aea_15c1_GetTerrainImprovements(x, y);
                    int visibility = (int)(gd.MapVisibility[x, y] & (1 << gd.HumanPlayerID));

                    tiles[y][x] = new
                    {
                        t = terrain,
                        i = improvements,
                        v = visibility != 0 ? 1 : 0
                    };
                }
            }
            return tiles;
        }

        private static object[] BuildCities(GameData gd)
        {
            var list = new List<object>();
            for (int i = 0; i < 128; i++)
            {
                var city = gd.Cities[i];
                if (city.PlayerID < 0 || city.ActualSize <= 0) continue;
                list.Add(new
                {
                    id = i,
                    name = gd.CityNames[city.NameID].TrimEnd(' ', '\x0'),
                    x = city.Position.X,
                    y = city.Position.Y,
                    size = city.ActualSize,
                    playerID = city.PlayerID,
                    production = city.CurrentProductionID,
                });
            }
            return list.ToArray();
        }

        private static object[] BuildUnits(GameData gd)
        {
            var list = new List<object>();
            for (int p = 0; p < 8; p++)
            {
                if ((gd.ActiveCivilizations & (1 << p)) == 0) continue;
                var player = gd.Players[p];
                for (int u = 0; u < 128; u++)
                {
                    var unit = player.Units[u];
                    if (unit.UnitType == UnitTypeEnum.None) continue;
                    var typeIdx = (int)unit.UnitType;
                    var unitName = typeIdx >= 0 && typeIdx < gd.Units.Length
                        ? gd.Units[typeIdx].Name.TrimEnd(' ', '\x0')
                        : unit.UnitType.ToString();
                    list.Add(new
                    {
                        id = u,
                        playerID = p,
                        type = typeIdx,
                        name = unitName,
                        x = unit.Position.X,
                        y = unit.Position.Y,
                        status = unit.Status,
                        moves = unit.RemainingMoves,
                    });
                }
            }
            return list.ToArray();
        }

        private static object[] BuildPlayers(GameData gd)
        {
            var list = new List<object>();
            for (int i = 0; i < 8; i++)
            {
                if ((gd.ActiveCivilizations & (1 << i)) == 0) continue;
                var p = gd.Players[i];
                list.Add(new
                {
                    id = i,
                    name = p.Name,
                    nationality = p.Nationality,
                    coins = p.Coins,
                    researchID = p.ResearchTechnologyID,
                    score = p.Score,
                    cityCount = p.CityCount,
                    governmentType = p.GovernmentType,
                });
            }
            return list.ToArray();
        }
    }
}
