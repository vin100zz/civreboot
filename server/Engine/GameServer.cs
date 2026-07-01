// Singleton — holds one game session for the lifetime of the server.
using Microsoft.Extensions.Configuration;

namespace OpenCivOne.Server
{
    public class GameServer : IDisposable
    {
        private readonly GameSession _session;

        public GameServer(IConfiguration config)
        {
            var civPath = config["CivPath"] ?? @"C:\V\Abandonware-France\Civilization\C\Civ\";
            if (!civPath.EndsWith(Path.DirectorySeparatorChar))
                civPath += Path.DirectorySeparatorChar;

            Console.WriteLine($"[GameServer] CivPath = {civPath}");
            Console.WriteLine($"[GameServer] Session {((_session = new GameSession(civPath)).SessionId)} started");
        }

        public string GetState() => _session.GetState();

        public string InjectAndWait(PlayerAction action) => _session.InjectAndWait(action);

        public string RevealMap() => _session.RevealMap();

        public void Dispose() => _session.Dispose();
    }
}
