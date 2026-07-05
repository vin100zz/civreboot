// The engine never records "which tech did we just discover" anywhere — it only
// tracks which techs are known so far (DiscoveredTechnologyFlags, a 5-word bitmask
// per player) and a running count (DiscoveredTechnologyCount). AI players in
// particular have no named "current research" at all until the instant it
// completes (see StateSerializer.cs's research.name comment), so the *only* way to
// show "last discovered: X" for them is to notice a newly-set bit ourselves.
//
// Update() must be called once per poll (see GameSession.GetState) so it can diff
// against the previous snapshot; a fresh GameSession (new game) starts with no
// snapshot, so the first poll after a new game establishes the baseline without
// reporting anything as "just discovered".
namespace OpenCivOne.Server
{
    public class TechDiscoveryTracker
    {
        private readonly ushort[]?[] _previousFlags = new ushort[]?[8];
        public string?[] LastDiscovered { get; } = new string?[8];

        public void Update(OpenCivOneGame game)
        {
            var gd = game.GameData;
            for (int p = 0; p < 8; p++)
            {
                if ((gd.ActiveCivilizations & (1 << p)) == 0) continue;
                var flags = gd.Players[p].DiscoveredTechnologyFlags;
                var prev = _previousFlags[p];

                if (prev != null)
                {
                    for (int word = 0; word < flags.Length; word++)
                    {
                        int newlySet = flags[word] & ~prev[word];
                        if (newlySet == 0) continue;

                        for (int bit = 0; bit < 16; bit++)
                        {
                            if ((newlySet & (1 << bit)) == 0) continue;
                            int techID = (word * 16) + bit;
                            if (techID < gd.TechnologyAdvances.Length)
                                LastDiscovered[p] = gd.TechnologyAdvances[techID].Name;
                        }
                    }
                }

                _previousFlags[p] = (ushort[])flags.Clone();
            }
        }
    }
}
