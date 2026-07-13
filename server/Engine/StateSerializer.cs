// Phase 3 — Serializes OpenCivOne GameData to a JSON-friendly DTO.
// Called after each game state change to broadcast to the web client.
using System.Text.Json;
using OpenCivOne.Graphics;

namespace OpenCivOne.Server
{
    public static class StateSerializer
    {
        public static string Serialize(OpenCivOneGame game, string?[]? lastDiscoveredTech = null, string pendingAction = "")
        {
            var gd = game.GameData;

            var tiles = BuildTileArray(game);
            var cities = BuildCities(game);
            var units = BuildUnits(gd);
            var players = BuildPlayers(game, lastDiscoveredTech);

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
                    // GetTerrainType only ever returns a base type (0-11) — special resources
                    // aren't stored per-tile, they're a deterministic function of (x, y,
                    // RandomSeed) checked on the fly (F0_2aea_1836_CellHasSpecialResource), and
                    // each base terrain has exactly one resource variant at baseType + 12
                    // (matches TerrainTypeEnum's ordering, e.g. Desert=0/Oasis=12, Forest=3/Game=15,
                    // and the original drawing code's Array_d4ce[(int)terrainType + 16] lookup).
                    int baseTerrain = (int)mm.GetTerrainType(x, y);
                    int terrain = mm.F0_2aea_1836_CellHasSpecialResource(x, y) ? baseTerrain + 12 : baseTerrain;
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

                // Buildings already completed in this city (Wonders included — both are
                // tracked in the same City.Improvements set). Sorted by name since
                // BHashSet doesn't guarantee build order.
                var improvements = city.Improvements
                    .Select(imp => gd.GetImprovementType((int)imp).Name.TrimEnd(' ', '\x0'))
                    .Where(n => n.Length > 0 && n != "NONE")
                    .OrderBy(n => n)
                    .ToArray();

                list.Add(new
                {
                    id = i,
                    name = gd.CityNames[city.NameID].TrimEnd(' ', '\x0'),
                    x = city.Position.X,
                    y = city.Position.Y,
                    size = city.ActualSize,
                    playerID = city.PlayerID,
                    production = city.CurrentProductionID,
                    improvements,
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
                        produced = economy.ShieldsProduced,
                        consumed = economy.ShieldsConsumedByUnits,
                        net = economy.ShieldsProduced - economy.ShieldsConsumedByUnits,
                        stored = (int)city.ShieldsCount,
                        current = economy.ProductionName,
                        cost = economy.ProductionCost,
                    },
                    trade = new
                    {
                        produced = economy.TradeProduced,
                        consumed = economy.TradeLostToCorruption,
                        net = economy.TradeProduced - economy.TradeLostToCorruption,
                        gold = economy.Gold,
                        science = economy.Science,
                        luxury = economy.Luxury,
                    },
                    unitsSupported = economy.UnitsSupported,
                    citizens = new
                    {
                        happy = economy.Happy,
                        normal = economy.ContentNormal,
                        unhappy = economy.Unhappy,
                        taxCollectors = economy.TaxCollectors,
                        scientists = economy.Scientists,
                        entertainers = economy.Entertainers,
                    },
                    workTiles = BuildCityWorkTiles(game, i).Select(t => new
                    {
                        dx = t.Dx,
                        dy = t.Dy,
                        worked = t.Worked,
                        food = t.Food,
                        shields = t.Shields,
                        trade = t.Trade,
                    }),
                });
            }
            return list.ToArray();
        }

        private readonly struct CityWorkTile
        {
            public int Dx { get; init; }
            public int Dy { get; init; }
            public bool Worked { get; init; }
            public int Food { get; init; }
            public int Shields { get; init; }
            public int Trade { get; init; }
        }

        // Per-tile yields for the 21-tile "fat cross" a city can work (game.CityOffsets;
        // index 20 is the city center, always worked for free) — the same tiles/formula
        // ComputeCityEconomy sums into totals below, exposed per-tile here so the client can
        // draw the city popup's work-tile mini-map (terrain/improvements for each tile are
        // read from the already-loaded main map client-side, keyed by cityX+dx/cityY+dy).
        private static CityWorkTile[] BuildCityWorkTiles(OpenCivOneGame game, int cityID)
        {
            var gd = game.GameData;
            var city = gd.Cities[cityID];
            var cw = game.CityWorker;
            var tiles = new CityWorkTile[21];
            for (int j = 0; j < 21; j++)
            {
                bool worked = (city.WorkerFlags & (uint)(1 << j)) != 0 || j == 20;
                int x = city.Position.X + game.CityOffsets[j].X;
                int y = city.Position.Y + game.CityOffsets[j].Y;

                tiles[j] = new CityWorkTile
                {
                    Dx = game.CityOffsets[j].X,
                    Dy = game.CityOffsets[j].Y,
                    Worked = worked,
                    Food = cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Food),
                    Shields = cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Production),
                    Trade = cw.F0_1d12_6abc_GetCityResourceCount(city.PlayerID, cityID, x, y, CityResourceTypeEnum.Trade),
                };
            }
            return tiles;
        }

        private readonly struct CityEconomy
        {
            public int FoodProduced { get; init; }
            public int FoodConsumed { get; init; }
            public int FoodNeededToGrow { get; init; }
            public int ShieldsProduced { get; init; }
            public int ShieldsConsumedByUnits { get; init; }
            public string ProductionName { get; init; }
            public int ProductionCost { get; init; }
            public int TradeProduced { get; init; }
            public int TradeLostToCorruption { get; init; }
            public int Gold { get; init; }
            public int Science { get; init; }
            public int Luxury { get; init; }
            public int UnitsSupported { get; init; }
            public int Happy { get; init; }
            public int ContentNormal { get; init; }
            public int Unhappy { get; init; }
            public int TaxCollectors { get; init; }
            public int Scientists { get; init; }
            public int Entertainers { get; init; }
        }

        // Recomputes a city's per-turn economy read-only, for display purposes.
        // Mirrors CityWorker.F0_1d12_0045_ProcessCityState's calculations (the function
        // that actually applies these each turn), reusing the same underlying formulas/
        // helpers where practical, but as a fresh, independent, side-effect-free query —
        // it does not touch City/Player fields.
        //
        // Known simplifications vs the exact in-game formula (kept out to limit scope/risk):
        //  - Trade routes / caravan bonuses between cities are not included.
        // Typically small relative to a city's base output.
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

            // --- Specialists (CityWorker.cs's SpecialWorkerFlags: 2 bits/slot, up to 8
            // slots) --- Citizens not assigned to one of the 20 tile slots become
            // specialists; each slot's 2-bit value is 1=TaxCollector, 2=Scientist,
            // 3=Entertainer (see F0_1d12_6dcc_GetWorkerCountByType and its callers, which
            // add +2 gold/science/luxury per matching specialist — CityWorker.cs ~line
            // 1378-1384).
            int workingCitizens = 0;
            for (int j = 0; j < 20; j++)
                if ((city.WorkerFlags & (uint)(1 << j)) != 0) workingCitizens++;
            int specialistCount = Math.Max(0, city.ActualSize - workingCitizens);

            int taxCollectors = 0, scientists = 0, entertainers = 0;
            for (int slot = 0; slot < specialistCount && slot < 8; slot++)
            {
                int type = (city.SpecialWorkerFlags >> (slot * 2)) & 0x3;
                if (type == 1) taxCollectors++;
                else if (type == 2) scientists++;
                else if (type == 3) entertainers++;
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
            // Shield upkeep for supported units (CityWorker.cs ~line 1762-1783: Var_deb8/
            // Var_d2f6). Each city gives ActualSize free unit-slots (Diplomats/Caravans never
            // count); a unit beyond that always costs 1 shield/turn. Units *within* the free
            // slots are normally free too — but only under Anarchy/Despotism (GovernmentType
            // 0-1): Monarchy and up bill every supported unit 1 shield/turn, free slots or not.
            int shieldUpkeep = 0;
            int supportedIndex = 0;
            foreach (var unit in player.Units)
            {
                if (unit.UnitType == UnitTypeEnum.None || unit.HomeCityID != cityID) continue;
                unitsSupported++;
                if (unit.UnitType == UnitTypeEnum.Settler) settlerCount++;

                if (unit.UnitType == UnitTypeEnum.Diplomat || unit.UnitType == UnitTypeEnum.Caravan) continue;
                supportedIndex++;
                bool withinFreeLimit = supportedIndex <= city.ActualSize;
                if (!withinFreeLimit || player.GovernmentType > 1)
                    shieldUpkeep++;
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

            // Specialist bonuses (+2 each) apply after the base trade split but before
            // MarketPlace/Bank's +50%, matching CityWorker.cs's instruction order exactly.
            gold += taxCollectors * 2;
            science += scientists * 2;
            luxury += entertainers * 2;

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

            var mood = ComputeCitizenMood(game, cityID, luxury, specialistCount);

            return new CityEconomy
            {
                FoodProduced = food,
                FoodConsumed = foodConsumed,
                FoodNeededToGrow = foodNeededToGrow,
                ShieldsProduced = shields,
                ShieldsConsumedByUnits = shieldUpkeep,
                ProductionName = productionName,
                ProductionCost = productionCost,
                TradeProduced = trade,
                TradeLostToCorruption = corruption,
                Gold = gold,
                Science = scienceTotal,
                Luxury = luxury,
                UnitsSupported = unitsSupported,
                Happy = mood.Happy,
                ContentNormal = mood.Normal,
                Unhappy = mood.Unhappy,
                TaxCollectors = taxCollectors,
                Scientists = scientists,
                Entertainers = entertainers,
            };
        }

        private readonly struct CitizenMood
        {
            public int Happy { get; init; }
            public int Normal { get; init; }
            public int Unhappy { get; init; }
        }

        // Faithful reproduction of CityWorker.cs's happiness pass (~line 1420-1737 —
        // F0_1d12_0045_ProcessCityState). The engine only keeps this as transient
        // per-turn scratch state (Var_70e4 = unhappy, Var_70e2 = happy, reset and
        // recomputed each time a city is processed), never stored per-city — so it has
        // to be recomputed independently here to be readable at arbitrary times (e.g.
        // when the player opens the city popup mid-turn), the same way ComputeCityEconomy
        // already re-derives food/shields/trade instead of reading transient fields.
        //
        // Known simplifications:
        //  - Martial law / war-weariness unit counting uses this player's units at/away
        //    from the city directly, rather than the original's cell-linked-list walk
        //    and City.Unknown[] references (a UI-facing cache we don't reproduce).
        private static CitizenMood ComputeCitizenMood(OpenCivOneGame game, int cityID, int luxury, int specialistCount)
        {
            var gd = game.GameData;
            var city = gd.Cities[cityID];
            var player = gd.Players[city.PlayerID];
            var cw = game.CityWorker;
            var gt = game.GameTools;

            // --- Initial unhappy baseline (CityWorker.cs ~line 1431-1443) ---
            int unhappy;
            if (city.PlayerID == gd.HumanPlayerID)
            {
                int e = 14 - (gd.DifficultyLevel * 2);
                e = ((player.GovernmentType / 2) + 2) * (e / 2);
                unhappy = (((cityID % e) + player.CityCount - e) / e) + city.ActualSize + gd.DifficultyLevel - 6;
            }
            else
            {
                unhappy = city.ActualSize - 3;
            }

            int overflow = 0;
            if (city.ActualSize < unhappy)
            {
                overflow = unhappy - city.ActualSize;
                unhappy = city.ActualSize;
            }

            // --- Happy budget from luxury (line 1469) ---
            int happy = luxury / 2;

            // --- Building/tech reductions to unhappy (line 1491-1557) ---
            if (city.HasImprovement(ImprovementEnum.Colosseum)) unhappy -= 3;

            if (game.Segment_1ade.F0_1ade_22b5_PlayerHasTechnology(city.PlayerID, TechnologyAdvanceEnum.Religion) &&
                city.HasImprovement(ImprovementEnum.Cathedral))
            {
                unhappy -= cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.MichelangelosChapel) ? 6 : 4;
            }

            if (city.HasImprovement(ImprovementEnum.Temple))
            {
                if (game.Segment_1ade.F0_1ade_22b5_PlayerHasTechnology(city.PlayerID, TechnologyAdvanceEnum.Mysticism))
                    unhappy -= 2;
                else if (game.Segment_1ade.F0_1ade_22b5_PlayerHasTechnology(city.PlayerID, TechnologyAdvanceEnum.CeremonialBurial))
                    unhappy -= 1;

                if (cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.Oracle))
                {
                    unhappy -= game.Segment_1ade.F0_1ade_22b5_PlayerHasTechnology(city.PlayerID, TechnologyAdvanceEnum.Mysticism) ? 2 : 1;
                }
            }

            // --- Martial law (GovernmentType<4) vs war-weariness (line 1585-1666) ---
            if (player.GovernmentType < 4)
            {
                int militiaInCity = 0;
                foreach (var unit in player.Units)
                {
                    if (unit.UnitType == UnitTypeEnum.None) continue;
                    if (unit.Position.X != city.Position.X || unit.Position.Y != city.Position.Y) continue;
                    if (gd.Units[(int)unit.UnitType].AttackStrength == 0) continue;
                    militiaInCity++;
                }
                militiaInCity = Math.Min(militiaInCity, 3);
                unhappy -= gt.F0_2dc4_007c_CheckValueRange(militiaInCity, 0, unhappy);
            }
            else
            {
                int awayMilitaryUnits = 0;
                foreach (var unit in player.Units)
                {
                    if (unit.UnitType == UnitTypeEnum.None || unit.HomeCityID != cityID) continue;
                    if (unit.UnitType == UnitTypeEnum.Diplomat || unit.UnitType == UnitTypeEnum.Caravan) continue;
                    if (gd.Units[(int)unit.UnitType].AttackStrength == 0) continue;
                    bool isAway = gd.Units[(int)unit.UnitType].MovementType == UnitMovementTypeEnum.Air ||
                        unit.Position.X != city.Position.X || unit.Position.Y != city.Position.Y;
                    if (isAway) awayMilitaryUnits++;
                }

                int weariness = cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.WomensSuffrage) ? 1 : 0;
                if (player.GovernmentType == 5) weariness++; // Democracy
                unhappy += weariness * awayMilitaryUnits;
            }

            // --- Wonder adjustments (line 1685-1714) ---
            if (cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.HangingGardens)) happy++;
            if (cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.CureForCancer)) happy++;
            if (gd.WonderCityID[(int)WonderEnum.ShakespearesTheatre] == cityID) unhappy = 0;
            if (cw.F0_1d12_6c97_PlayerHasWonder(city.PlayerID, WonderEnum.JSBachsCathedral))
            {
                var bachCity = gd.Cities[gd.WonderCityID[(int)WonderEnum.JSBachsCathedral]];
                if (game.MapManagement.F0_2aea_1942_GetGroupID(city.Position.X, city.Position.Y) ==
                    game.MapManagement.F0_2aea_1942_GetGroupID(bachCity.Position.X, bachCity.Position.Y))
                {
                    unhappy -= 2;
                }
            }

            // --- Normalize against city size (F0_1d12_6dfe, called repeatedly in the
            // original; the net effect only depends on the final values) ---
            while (overflow > 0 && unhappy < overflow) { overflow--; unhappy++; }
            happy = gt.F0_2dc4_007c_CheckValueRange(happy, 0, city.ActualSize);
            unhappy = gt.F0_2dc4_007c_CheckValueRange(unhappy, 0, city.ActualSize);

            int nonSpecialistSize = gt.F0_2dc4_007c_CheckValueRange(city.ActualSize - specialistCount, 0, 99);
            while (happy + unhappy > nonSpecialistSize)
            {
                if (overflow > 0) overflow--;
                else happy = gt.F0_2dc4_007c_CheckValueRange(happy - 1, 0, city.ActualSize);
                unhappy = gt.F0_2dc4_007c_CheckValueRange(unhappy - 1, 0, city.ActualSize);
            }

            int normal = city.ActualSize - specialistCount - happy - unhappy;
            return new CitizenMood { Happy = happy, Normal = normal, Unhappy = unhappy };
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

        // Real, one-time-discoverable technologies. TechnologyAdvances has 73 entries;
        // the last 5 (FutureTechnology1-5) are the repeatable post-game techs, not part
        // of a finite "total" — see GameData.cs's technologyAdvanceTypes list.
        private const int RealTechnologyCount = 68;

        private static object[] BuildPlayers(OpenCivOneGame game, string?[]? lastDiscoveredTech)
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
                //
                // ResearchTechnologyID (the *named* target) is a human-only concept in the
                // original game: AI players accumulate ResearchProgress the same way, but the
                // engine only picks (and reveals) which tech they get at the moment research
                // completes, never before — so `name` stays null for AI (and for the human
                // before their first choice). Progress/total are always shown regardless, since
                // they don't depend on a chosen target. discoveredCount/totalTechCount and
                // lastDiscovered (see TechDiscoveryTracker) let the client show an overall
                // "N/68 technologies" bar and name the last one found even when there's no
                // named current target to display.
                int varD2de = gd.Year < 0 ? 0 : Math.Clamp(gd.MaximumTechnologyCount - (gd.TurnCount / 9), 0, 6);
                int total = Math.Max((p.DiscoveredTechnologyCount * (gd.Year < 0 ? 1 : 2)) *
                    Math.Max((gd.DifficultyLevel * 2) + varD2de + 6, 11 - p.DiscoveredTechnologyCount), 1);
                string? researchName = (p.ResearchTechnologyID != -1 && p.ResearchTechnologyID < gd.TechnologyAdvances.Length)
                    ? gd.TechnologyAdvances[p.ResearchTechnologyID].Name
                    : null;
                var research = new
                {
                    name = researchName,
                    progress = Math.Clamp((int)p.ResearchProgress, 0, total),
                    total,
                    discoveredCount = (int)p.DiscoveredTechnologyCount,
                    totalTechCount = RealTechnologyCount,
                    lastDiscovered = lastDiscoveredTech?[i],
                };

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
