// Phase 3 — Serializes OpenCivOne GameData to a JSON-friendly DTO.
// Called after each game state change to broadcast to the web client.
using System.Text.Json;
using OpenCivOne.Graphics;

namespace OpenCivOne.Server
{
    public static class StateSerializer
    {
        public static string Serialize(OpenCivOneGame game, string pendingAction = "")
        {
            var gd = game.GameData;

            var tiles = BuildTileArray(game);
            var cities = BuildCities(game);
            var units = BuildUnits(gd);
            var players = BuildPlayers(game);

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
                    // Despite the names, F0_2aea_1585_GetVisibleTerrainImprovements is the raw/
                    // ground-truth improvements layer (same storage SetTerrainImprovements writes
                    // to), while F0_2aea_15c1_GetTerrainImprovements reads a separate "revealed to
                    // player" copy that's only synced by F0_2aea_1601_UpdateVisibleCellStatus — a
                    // call that's part of the original screen-drawing path and never happens in
                    // this headless server, so that copy stays stale (basically always 0). Our own
                    // fog-of-war is the "v" field below (tiles with v=0 aren't drawn client-side),
                    // so reading the raw layer here is safe and actually reflects real improvements.
                    int improvements = (int)mm.F0_2aea_1585_GetVisibleTerrainImprovements(x, y);
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

        private static object[] BuildCities(OpenCivOneGame game)
        {
            var gd = game.GameData;
            var list = new List<object>();
            for (int i = 0; i < 128; i++)
            {
                var city = gd.Cities[i];
                if (city.PlayerID < 0 || city.ActualSize <= 0) continue;

                var economy = ComputeCityEconomy(game, i);

                list.Add(new
                {
                    id = i,
                    name = gd.CityNames[city.NameID].TrimEnd(' ', '\x0'),
                    x = city.Position.X,
                    y = city.Position.Y,
                    size = city.ActualSize,
                    playerID = city.PlayerID,
                    production = city.CurrentProductionID,
                    food = new
                    {
                        stored = (int)city.FoodCount,
                        produced = economy.FoodProduced,
                        consumed = economy.FoodConsumed,
                        net = economy.FoodProduced - economy.FoodConsumed,
                        neededToGrow = economy.FoodNeededToGrow,
                    },
                    shields = new
                    {
                        producedPerTurn = economy.ShieldsProduced,
                        stored = (int)city.ShieldsCount,
                        current = economy.ProductionName,
                        cost = economy.ProductionCost,
                    },
                    trade = new
                    {
                        gold = economy.Gold,
                        science = economy.Science,
                        luxury = economy.Luxury,
                    },
                    unitsSupported = economy.UnitsSupported,
                });
            }
            return list.ToArray();
        }

        private readonly struct CityEconomy
        {
            public int FoodProduced { get; init; }
            public int FoodConsumed { get; init; }
            public int FoodNeededToGrow { get; init; }
            public int ShieldsProduced { get; init; }
            public string ProductionName { get; init; }
            public int ProductionCost { get; init; }
            public int Gold { get; init; }
            public int Science { get; init; }
            public int Luxury { get; init; }
            public int UnitsSupported { get; init; }
        }

        // Recomputes a city's per-turn economy read-only, for display purposes.
        // Mirrors CityWorker.F0_1d12_0045_ProcessCityState's calculations (the function
        // that actually applies these each turn), reusing the same underlying formulas/
        // helpers where practical, but as a fresh, independent, side-effect-free query —
        // it does not touch City/Player fields.
        //
        // Known simplifications vs the exact in-game formula (kept out to limit scope/risk):
        //  - Trade routes / caravan bonuses between cities are not included.
        //  - Manually-assigned specialist citizens (tax collectors/scientists/entertainers,
        //    via City.Unknown[]) are not included in the gold/science/luxury totals.
        // Both are typically small relative to a city's base output.
        private static CityEconomy ComputeCityEconomy(OpenCivOneGame game, int cityID)
        {
            var gd = game.GameData;
            var city = gd.Cities[cityID];
            var player = gd.Players[city.PlayerID];
            var cw = game.CityWorker;

            // --- Food / Shields / Trade totals from worked tiles ---
            // Bit j (0-19) of WorkerFlags = CityOffsets[j] is worked by a citizen;
            // CityOffsets[20] (the city center) is always worked for free.
            int food = 0, shields = 0, trade = 0;
            for (int j = 0; j < 21; j++)
            {
                bool worked = (city.WorkerFlags & (uint)(1 << j)) != 0 || j == 20;
                if (!worked) continue;

                int x = city.Position.X + game.CityOffsets[j].X;
                int y = city.Position.Y + game.CityOffsets[j].Y;

                food += cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Food);
                shields += cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Production);
                trade += cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Trade);
            }

            // --- Growth/production multiplier (CityWorker.cs ~line 217-230) ---
            // 10 for the human player; a difficulty-based, slightly AI-favoring value
            // otherwise (the exact in-game formula also shaves 2 more off it in a rare
            // end-game ranking condition, skipped here as a minor simplification).
            int growthMultiplier = city.PlayerID == gd.HumanPlayerID
                ? 10
                : Math.Max(1, -((gd.DifficultyLevel * 2) - 16));

            // --- Food consumption: 2/citizen + settler upkeep (CityWorker.cs line 768) ---
            int settlerCost = player.GovernmentType <= 1 ? 1 : 2; // Anarchy/Despotism cheaper
            int settlerCount = 0;
            int unitsSupported = 0;
            foreach (var unit in player.Units)
            {
                if (unit.UnitType == UnitTypeEnum.None || unit.HomeCityID != cityID) continue;
                unitsSupported++;
                if (unit.UnitType == UnitTypeEnum.Settler) settlerCount++;
            }
            int foodConsumed = (city.ActualSize * 2) + (settlerCount * settlerCost);
            int foodNeededToGrow = (city.ActualSize + 1) * growthMultiplier;

            // --- Trade split into gold/science/luxury (CityWorker.cs line ~1304-1420) ---
            int distanceToPalace = FindDistanceToPalace(gd, game, city.PlayerID, city.Position);
            if (player.GovernmentType == 3) distanceToPalace = 10; // Communism: flat corruption

            int corruption = (trade * distanceToPalace * 3) / ((player.GovernmentType * 20) + 80);
            if (city.HasImprovement(ImprovementEnum.Courthouse) || city.HasImprovement(ImprovementEnum.Palace))
                corruption /= 2;
            if (player.GovernmentType == 5) corruption = 0; // Democracy: no corruption

            var gt = game.GameTools;
            int luxury = gt.F0_2dc4_007c_CheckValueRange(
                (((10 - player.ScienceTaxRate - player.TaxRate) * (trade - corruption)) + 5) / 10,
                0, trade);
            int gold = gt.F0_2dc4_007c_CheckValueRange(
                ((player.TaxRate * (trade - corruption)) + 5) / 10,
                0, trade - luxury - corruption);
            int science = trade - luxury - gold - corruption;

            if (city.HasImprovement(ImprovementEnum.MarketPlace)) { luxury += luxury / 2; gold += gold / 2; }
            if (city.HasImprovement(ImprovementEnum.Bank)) { luxury += luxury / 2; gold += gold / 2; }

            int scienceTotal = science;
            bool hasNewton = cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.IsaacNewtonsCollege);
            if (city.HasImprovement(ImprovementEnum.Library))
            {
                scienceTotal += science / 2;
                if (hasNewton) scienceTotal += science / 3;
            }
            if (city.HasImprovement(ImprovementEnum.University))
            {
                scienceTotal += science / 2;
                if (hasNewton) scienceTotal += science / 3;
            }
            if (cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.CopernicusObservatory) &&
                gd.WonderCityID[(int)WonderEnum.CopernicusObservatory] == cityID)
            {
                scienceTotal += scienceTotal;
            }

            // --- Current production (unit if >=0, improvement/wonder if <0) ---
            string productionName;
            int productionCost;
            if (city.CurrentProductionID >= 0 && city.CurrentProductionID < gd.Units.Length)
            {
                var unitDef = gd.Units[city.CurrentProductionID];
                productionName = unitDef.Name.TrimEnd(' ', '\x0');
                productionCost = unitDef.Cost * growthMultiplier;
            }
            else if (city.CurrentProductionID < 0)
            {
                var impDef = gd.GetImprovementType(-city.CurrentProductionID);
                productionName = impDef.Name.TrimEnd(' ', '\x0');
                productionCost = impDef.Cost * growthMultiplier;
            }
            else
            {
                productionName = "";
                productionCost = 0;
            }

            return new CityEconomy
            {
                FoodProduced = food,
                FoodConsumed = foodConsumed,
                FoodNeededToGrow = foodNeededToGrow,
                ShieldsProduced = shields,
                ProductionName = productionName,
                ProductionCost = productionCost,
                Gold = gold,
                Science = scienceTotal,
                Luxury = luxury,
                UnitsSupported = unitsSupported,
            };
        }

        private static int FindDistanceToPalace(GameData gd, OpenCivOneGame game, int playerID, GPoint from)
        {
            int best = 999;
            for (int i = 0; i < 128; i++)
            {
                var c = gd.Cities[i];
                if (c.PlayerID != playerID || c.ActualSize <= 0) continue;
                if (!c.HasImprovement(ImprovementEnum.Palace)) continue;
                int d = game.GameTools.F0_2dc4_0289_GetShortestDistance(from, c.Position);
                if (d < best) best = d;
            }
            return best == 999 ? 0 : best;
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

        private static object[] BuildPlayers(OpenCivOneGame game)
        {
            var gd = game.GameData;
            var list = new List<object>();
            for (int i = 0; i < 8; i++)
            {
                if ((gd.ActiveCivilizations & (1 << i)) == 0) continue;
                var p = gd.Players[i];

                // Tech research progress — same formula as the in-game Science Report
                // screen (Overlay_14.cs F14_0000_014b_ScienceReport). Var_d2de there is a
                // per-turn value recomputed in Segment_1238's GameTurn (not player-specific):
                // 0 in the BC era, otherwise clamp(MaximumTechnologyCount - TurnCount/9, 0, 6).
                object? research = null;
                if (p.ResearchTechnologyID != -1 && p.ResearchTechnologyID < gd.TechnologyAdvances.Length)
                {
                    int varD2de = gd.Year < 0 ? 0 : Math.Clamp(gd.MaximumTechnologyCount - (gd.TurnCount / 9), 0, 6);
                    int total = (p.DiscoveredTechnologyCount * (gd.Year < 0 ? 1 : 2)) *
                        Math.Max((gd.DifficultyLevel * 2) + varD2de + 6, 11 - p.DiscoveredTechnologyCount);
                    research = new
                    {
                        name = gd.TechnologyAdvances[p.ResearchTechnologyID].Name,
                        progress = Math.Clamp((int)p.ResearchProgress, 0, Math.Max(total, 1)),
                        total = Math.Max(total, 1),
                    };
                }

                int govIdx = Math.Clamp((int)p.GovernmentType, 0, 5);

                list.Add(new
                {
                    id = i,
                    name = p.Name,
                    nationality = p.Nationality,
                    coins = p.Coins,
                    researchID = p.ResearchTechnologyID,
                    research,
                    score = p.Score,
                    cityCount = p.CityCount,
                    governmentType = p.GovernmentType,
                    governmentName = game.Array_1966[govIdx],
                    taxRate = p.TaxRate * 10,
                    scienceRate = p.ScienceTaxRate * 10,
                    luxuryRate = (10 - p.TaxRate - p.ScienceTaxRate) * 10,
                });
            }
            return list.ToArray();
        }
    }
}
