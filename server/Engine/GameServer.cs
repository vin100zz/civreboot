// Singleton — holds the current game session. NewGame() swaps in a fresh one.
using Microsoft.Extensions.Configuration;

namespace OpenCivOne.Server
{
    public class GameServer : IDisposable
    {
        private readonly string _civPath;
        private GameSession _session;

        public GameServer(IConfiguration config)
        {
            _civPath = config["CivPath"] ?? @"C:\V\Abandonware-France\Civilization\C\Civ\";
            if (!_civPath.EndsWith(Path.DirectorySeparatorChar))
                _civPath += Path.DirectorySeparatorChar;

            Console.WriteLine($"[GameServer] CivPath = {_civPath}");
            _session = new GameSession(_civPath);
            Console.WriteLine($"[GameServer] Session {_session.SessionId} started");
        }

        public string GetState() => _session.GetState();

        public string InjectAndWait(PlayerAction action) => _session.InjectAndWait(action);

        public string SetViewMode(int mode) => _session.SetViewMode(mode);

        // Starts a fresh game. The old session's engine thread has no cancellation
        // hook into the decompiled VCPU loop, so it's left running in the background
        // (harmless — IsBackground threads die with the process) rather than torn down.
        public string NewGame(NewGameOptions? options = null)
        {
            var old = _session;
            _session = new GameSession(_civPath, options);
            Console.WriteLine($"[GameServer] Session {_session.SessionId} started (replacing {old.SessionId})");
            old.Dispose();
            return _session.GetState();
        }

        public void Dispose() => _session.Dispose();
    }
}
