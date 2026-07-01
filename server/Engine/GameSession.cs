// One game instance. The game engine runs on a background thread.
// Human input comes via InjectAndWait() called directly from HTTP handlers.
using IRB.VirtualCPU;

namespace OpenCivOne.Server
{
    public class GameSession : IDisposable
    {
        public readonly string SessionId = Guid.NewGuid().ToString("N")[..8];

        private readonly OpenCivOneGame _game;
        private readonly CancellationTokenSource _cts = new();

        public GameSession(string civPath)
        {
            _game = new OpenCivOneGame(civPath);

            var gameThread = new Thread(() =>
            {
                try { _game.Start(); }
                catch (ApplicationExitException) { }
                catch (Exception ex) { Console.Error.WriteLine($"[Game] {ex}"); }
            })
            { IsBackground = true, Name = "GameEngine" };

            // Désactive les barbares proprement via le flag lu par F0_1238_0980() dans Segment_1238.cs.
            _game.NoBarbarians = true;
            Console.WriteLine("[Init] NoBarbarians = true");

            // Force LandMass=2 (Large) avant que GenerateMap() soit appelé.
            // "New Game" (MainCode case 0) hardcode LandMass=1 puis appelle GenerateMap() :
            // on écrase la variable en boucle jusqu'au démarrage du premier tour.
            var landmassThread = new Thread(() =>
            {
                while (_game.GameData.TurnCount == 0 && !_cts.Token.IsCancellationRequested)
                {
                    _game.Var_7ef6_PlanetLandMass = 2;
                    Thread.Sleep(50);
                }
                Console.WriteLine($"[LandMass] Carte générée avec LandMass={_game.Var_7ef6_PlanetLandMass}");
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
                Console.WriteLine("[AutoStart] Difficulty → Prince...");
                SelectMenuByKey('p'); // P = Prince
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
                // Forcer Prince (2) car l'injection de touches est trop fragile
                // face à EmptyKeyboardBufferAndClearMouse() du menu de difficulté
                Thread.Sleep(500);
                _game.GameData.DifficultyLevel = 2;
                Console.WriteLine($"[AutoStart] DifficultyLevel forcé = {_game.GameData.DifficultyLevel} (Prince)");
            })
            { IsBackground = true, Name = "AutoStart" };

            landmassThread.Start();
            gameThread.Start();
            autoStart.Start();
        }

        public string GetState() => StateSerializer.Serialize(_game);

        public string RevealMap()
        {
            var gd = _game.GameData;
            ushort bit = (ushort)(1 << gd.HumanPlayerID);
            for (int x = 0; x < 80; x++)
                for (int y = 0; y < 50; y++)
                    gd.MapVisibility[x, y] |= bit;
            return StateSerializer.Serialize(_game);
        }

        public string InjectAndWait(PlayerAction action)
        {
            if (action.Type == ActionType.EndTurn)
            {
                // Space skips ONE unit's turn. Keep pressing Space until TurnCount changes
                // (covers cases where the player has several units still waiting).
                // Alternate Space (skip unit) and Enter (confirm dialogs like city production).
                // Space alone gets stuck when the game is waiting for a production/research dialog.
                int startTurn = _game.GameData.TurnCount;
                var deadline = DateTime.UtcNow.AddSeconds(30);
                int tick = 0;
                while (_game.GameData.TurnCount == startTurn && DateTime.UtcNow < deadline)
                {
                    if (_game.Keys.Count == 0)
                    {
                        int key = (tick % 4 == 0) ? '\r' : ' ';
                        lock (VCPU.KeyboardLock)
                            _game.Keys.Enqueue(key);
                        tick++;
                    }
                    Thread.Sleep(150);
                }
                Thread.Sleep(200); // let state settle
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
            return StateSerializer.Serialize(_game);
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
