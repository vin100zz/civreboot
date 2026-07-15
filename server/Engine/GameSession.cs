// One game instance. The game engine runs on a background thread.
// Human input comes via InjectAndWait() called directly from HTTP handlers.
using IRB.VirtualCPU;
using OpenCivOne.Graphics;
using System.Text.Json;

namespace OpenCivOne.Server
{
    public class GameSession : IDisposable
    {
        public readonly string SessionId = Guid.NewGuid().ToString("N")[..8];

        private readonly OpenCivOneGame _game;
        private readonly CancellationTokenSource _cts = new();
        private readonly TechDiscoveryTracker _techTracker = new();

        // Which fog-of-war to serialize the map with: -1 = the human player's own
        // view (default), -2 = "view all" (no fog), 0-15 = spectate as that player.
        // Purely a display setting — never touches GameData.MapVisibility, so
        // switching back to "my view" always un-reveals everything (unlike the old
        // Reveal Map button, which permanently OR'd the human's visibility bitmask).
        private int _viewPlayerID = -1;

        public GameSession(string civPath, NewGameOptions? options = null)
        {
            var opts = NewGameOptions.Clamp(options ?? new NewGameOptions());

            _game = new OpenCivOneGame(civPath);

            var gameThread = new Thread(() =>
            {
                try { _game.Start(); }
                catch (ApplicationExitException) { }
                catch (Exception ex) { Console.Error.WriteLine($"[Game] {ex}"); }
            })
            { IsBackground = true, Name = "GameEngine" };

            // Désactive les barbares proprement via le flag lu par F0_1238_0980() dans Segment_1238.cs.
            _game.NoBarbarians = !opts.Barbarians;
            Console.WriteLine($"[Init] NoBarbarians = {_game.NoBarbarians}");

            // Earth / custom map: force Var_d76a_EarthMap so MapInitAndIntro.GenerateMap()
            // skips procedural generation entirely (see MapInitAndIntro.cs ~line 65) —
            // regardless of which menu item the autoStart burst below actually selects,
            // since MainCode's "Start a New Game" (case 0) and "EARTH" (case 2) both just
            // call the same GenerateMap(). CustomMap takes priority over Earth if both set.
            bool loadPrebuiltMap = opts.Earth || opts.CustomMap != null;
            if (opts.CustomMap != null)
            {
                var mapData = OpenCivOne.Server.Maps.CustomMapFormat.Load(opts.CustomMap);
                _game.CustomMapGrid = mapData.Grid;

                // Resolve the map's per-nationality start positions (keyed by name, e.g.
                // "Roman") to NationalityID slots (see GameData.cs's nationTypes list for
                // the id <-> name mapping) — this is what StartGameMenu.cs reads instead of
                // the real Array_35da when a custom map is active.
                int matched = 0;
                for (int i = 0; i < _game.GameData.Nations.Length; i++)
                {
                    string nationality = _game.GameData.Nations[i].Nationality;
                    if (!string.IsNullOrEmpty(nationality) && mapData.StartPositions.TryGetValue(nationality, out var pos))
                    {
                        _game.CustomMapStartPositions[i] = new GPoint(pos.X, pos.Y);
                        matched++;
                    }
                }
                Console.WriteLine($"[Map] Carte personnalisée chargée : {opts.CustomMap} ({matched} position(s) de départ définies)");
            }

            // Force LandMass/Age (et EarthMap) avant que GenerateMap() soit appelé.
            // "New Game" (MainCode case 0) hardcode LandMass=1/Age=1 puis appelle GenerateMap() :
            // on écrase les variables en boucle jusqu'au démarrage du premier tour.
            //
            // Also forces GameSettingFlags.AutoSave off here (it defaults to on —
            // GameSettings.cs's settingsValue = 2 has that bit set). The original engine's
            // own autosave (Segment_1238.cs, every 50 turns) is a blocking "Game has been
            // saved. Press key to continue." dialog + getch() with no headless equivalent;
            // we reimplement autosave ourselves in InjectAndWait below (same slot rotation,
            // no blocking, and it surfaces a result via pendingAction) instead of letting the
            // built-in one run redundantly and stall the engine thread every 50 turns.
            var landmassThread = new Thread(() =>
            {
                while (_game.GameData.TurnCount == 0 && !_cts.Token.IsCancellationRequested)
                {
                    _game.Var_7ef6_PlanetLandMass = opts.LandMass;
                    _game.Var_7efc_PlanetAge = opts.Age;
                    if (loadPrebuiltMap) _game.Var_d76a_EarthMap = true;
                    _game.GameData.GameSettingFlags.AutoSave = false;
                    Thread.Sleep(50);
                }
                Console.WriteLine($"[LandMass] Carte générée avec LandMass={_game.Var_7ef6_PlanetLandMass} Age={_game.Var_7efc_PlanetAge}");
            })
            { IsBackground = true, Name = "LandMassOverride" };

            // Auto-start: burst-inject Enter every 100ms to survive EmptyKeyboardBufferAndClearMouse
            var autoStart = new Thread(() =>
            {
                void Burst(int durationMs, int intervalMs = 100, int key = '\r')
                {
                    var deadline = DateTime.UtcNow.AddMilliseconds(durationMs);
                    while (DateTime.UtcNow < deadline && !_cts.Token.IsCancellationRequested)
                    {
                        lock (VCPU.KeyboardLock)
                            _game.Keys.Enqueue(key);
                        Thread.Sleep(intervalMs);
                    }
                }

                void SelectMenuByKey(char letter)
                {
                    // MenuBoxDialog matches menuItem[1] (second char, after leading space).
                    // Burst the letter long enough to survive EmptyKeyboardBufferAndClearMouse, then confirm.
                    Burst(2000, 100, char.ToLower(letter));
                    Burst(1000);
                }

                Thread.Sleep(1500);
                Console.WriteLine("[AutoStart] Blasting intro...");
                Burst(4000);
                Console.WriteLine("[AutoStart] Main menu...");
                Burst(1500);
                Console.WriteLine("[AutoStart] Waiting for map gen...");
                Thread.Sleep(6000);
                // Difficulty: 0=Chieftain 1=Warlord 2=Prince 3=King 4=Emperor
                char[] difficultyKeys = { 'c', 'w', 'p', 'k', 'e' };
                string[] difficultyNames = { "Chieftain", "Warlord", "Prince", "King", "Emperor" };
                Console.WriteLine($"[AutoStart] Difficulty → {difficultyNames[opts.Difficulty]}...");
                SelectMenuByKey(difficultyKeys[opts.Difficulty]);
                Thread.Sleep(300);
                Console.WriteLine("[AutoStart] Competition...");
                Burst(1000);
                Thread.Sleep(300);
                Console.WriteLine("[AutoStart] Tribe...");
                Burst(1000);
                Thread.Sleep(500);
                Console.WriteLine("[AutoStart] Start screen...");
                Burst(2000);
                Console.WriteLine("[AutoStart] Done — game loop should be running");
                // Force la difficulté choisie car l'injection de touches est trop fragile
                // face à EmptyKeyboardBufferAndClearMouse() du menu de difficulté
                Thread.Sleep(500);
                _game.GameData.DifficultyLevel = (short)opts.Difficulty;
                Console.WriteLine($"[AutoStart] DifficultyLevel forcé = {_game.GameData.DifficultyLevel} ({difficultyNames[opts.Difficulty]})");
            })
            { IsBackground = true, Name = "AutoStart" };

            landmassThread.Start();
            gameThread.Start();
            autoStart.Start();
        }

        private string Serialize(string pendingAction = "")
        {
            _techTracker.Update(_game);
            return StateSerializer.Serialize(_game, _techTracker.LastDiscovered, pendingAction, _viewPlayerID);
        }

        public string GetState() => Serialize();

        // mode: -1 = the human player's own view, -2 = view all (no fog), 0-15 = spectate
        // as that player. Just a display setting for future GetState()/InjectAndWait() calls.
        public string SetViewMode(int mode)
        {
            _viewPlayerID = mode;
            return Serialize();
        }

        // Save/load reuse the original game's own file format and slot convention
        // (CIVIL0.SVE..CIVIL9.SVE + a matching .MAP terrain bitmap, written straight to
        // ResourcePath, i.e. the configured Civ install directory) via GameLoadAndSave —
        // the same engine code the original DOS game's Save/Load menu called, just
        // invoked directly instead of through its menu/dialog UI. pendingAction carries
        // a human-readable result back to the client (an existing, previously-unused
        // state field — see StateSerializer.Serialize).
        public string SaveGame(int slot)
        {
            slot = Math.Clamp(slot, 0, 9);
            bool ok = _game.GameLoadAndSave.F11_0000_08f6_SaveGameData($"CIVIL{slot}.SVE");
            return Serialize(ok ? $"Game saved to slot {slot}." : $"Failed to save to slot {slot}.");
        }

        public string LoadGame(int slot)
        {
            slot = Math.Clamp(slot, 0, 9);
            bool ok = _game.GameLoadAndSave.F11_0000_083b_LoadGameData($"CIVIL{slot}.SVE");
            // The loaded save's discovered-tech bitmasks have nothing to do with whatever
            // game was running a moment ago — reset so the next Update() doesn't diff
            // against stale data and falsely report techs as "just discovered".
            if (ok)
            {
                _techTracker.Reset();
                // The save file carries its own GameSettingFlags (including AutoSave), which
                // may well be the original engine's default (on) — force it back off so we
                // don't reintroduce the blocking autosave dialog this session already disabled.
                _game.GameData.GameSettingFlags.AutoSave = false;
            }
            return Serialize(ok ? $"Game loaded from slot {slot}." : $"Failed to load slot {slot} (file may not exist).");
        }

        // Slot info for a save/load picker UI: 10 fixed slots (matching the original
        // game's own Save/Load menu), each either empty or described by the same
        // formatted string the original menu showed (difficulty, player, nation, year).
        public string ListSaves()
        {
            var slots = new List<object>();
            for (int i = 0; i < 10; i++)
            {
                string info = _game.GameLoadAndSave.F11_0000_0103_ReadGameInfo($"CIVIL{i}.SVE", out bool exists);
                slots.Add(new { slot = i, exists, label = info.Trim() });
            }
            return JsonSerializer.Serialize(slots);
        }

        public string InjectAndWait(PlayerAction action)
        {
            if (action.Type == ActionType.EndTurn)
            {
                // Space skips ONE unit's turn. Keep pressing Space until TurnCount changes
                // (covers cases where the player has several units still waiting).
                // Alternate Space (skip unit) and Enter (confirm dialogs like city production).
                // Space alone gets stuck when the game is waiting for a production/research dialog.
                //
                // Every 6th tick sends a Down-arrow (DOS extended scancode 0x5000) instead —
                // MenuBoxDialog.cs's interactive menu loop starts with option 0 selected, and if
                // that option happens to be disabled (Var_b276_MenuBoxDisabledOptions), Enter/Space
                // are silently swallowed forever (the loop only exits on Enter/Space when the
                // *currently selected* option isn't disabled) — a plain Enter/Space keep-alive can
                // never recover from that, and the turn would stall until the deadline below, every
                // single time it's retried. Down-arrow unconditionally moves the selection forward
                // (never past the last option, never re-triggers the disabled one once past it), so
                // interleaving it walks off a disabled default within a few ticks.
                int startTurn = _game.GameData.TurnCount;
                var deadline = DateTime.UtcNow.AddSeconds(30);
                int tick = 0;
                while (_game.GameData.TurnCount == startTurn && DateTime.UtcNow < deadline)
                {
                    if (_game.Keys.Count == 0)
                    {
                        int key = (tick % 6 == 5) ? 0x5000 : (tick % 4 == 0) ? '\r' : ' ';
                        lock (VCPU.KeyboardLock)
                            _game.Keys.Enqueue(key);
                        tick++;
                    }
                    Thread.Sleep(150);
                }
                Thread.Sleep(200); // let state settle

                // Auto-save: mirrors the original engine's own cadence/slot rotation
                // (Segment_1238.cs, every 50 turns, rotating through 6 slots starting at 4 —
                // same convention manual saves use for slots 0-3) but calls the save data
                // writer directly instead of the original's blocking dialog + getch(), and
                // surfaces the result via pendingAction instead of a screen the client can't see.
                int turnCount = _game.GameData.TurnCount;
                if (turnCount != startTurn && turnCount > 0 && turnCount % 50 == 0)
                {
                    int slot = 4 + (((turnCount / 50) - 1) % 6);
                    bool ok = _game.GameLoadAndSave.F11_0000_08f6_SaveGameData($"CIVIL{slot}.SVE");
                    return Serialize(ok ? $"Auto-saved to slot {slot}." : $"Auto-save to slot {slot} failed.");
                }
            }
            else
            {
                // getch() sleeps 200ms between checks, so the key may sit in the queue
                // up to 200ms before being consumed. After consumption the game still needs
                // a moment to update GameData (unit position, etc.).
                // Strategy: wait for consumption (up to 400ms), then 250ms processing buffer.
                InjectAction(action);
                var deadline = DateTime.UtcNow.AddMilliseconds(400);
                while (_game.Keys.Count > 0 && DateTime.UtcNow < deadline)
                    Thread.Sleep(10);
                Thread.Sleep(400);
            }
            return Serialize();
        }

        private void InjectAction(PlayerAction action)
        {
            Console.WriteLine($"[Action] {action.Type} int={action.IntParam} str={action.StringParam}");
            switch (action.Type)
            {
                case ActionType.KeyPress when action.IntParam.HasValue:
                    lock (VCPU.KeyboardLock)
                        _game.Keys.Enqueue(action.IntParam.Value);
                    break;

                case ActionType.MoveUnit when action.IntParam.HasValue:
                    // CheckPlayerTurn.cs Label1162: 0x01=N 0x02=NE 0x03=E 0x04=SE 0x05=S 0x06=SW 0x07=W 0x08=NW
                    int key = action.IntParam.Value switch
                    {
                        8 => 0x01, 9 => 0x02, 6 => 0x03, 3 => 0x04,
                        2 => 0x05, 1 => 0x06, 4 => 0x07, 7 => 0x08,
                        _ => ' '
                    };
                    lock (VCPU.KeyboardLock)
                        _game.Keys.Enqueue(key);
                    break;

                case ActionType.EndTurn:
                    lock (VCPU.KeyboardLock)
                        _game.Keys.Enqueue(' ');
                    break;

                case ActionType.FoundCity:
                    lock (VCPU.KeyboardLock)
                        _game.Keys.Enqueue('b');
                    break;

                case ActionType.TextInput when action.StringParam != null:
                    foreach (char c in action.StringParam)
                        lock (VCPU.KeyboardLock)
                            _game.Keys.Enqueue(c);
                    lock (VCPU.KeyboardLock)
                        _game.Keys.Enqueue('\r');
                    break;
            }
        }

        public void Dispose() => _cts.Cancel();
    }
}
