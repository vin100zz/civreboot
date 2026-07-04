namespace OpenCivOne.Server
{
    // Mirrors the choices offered by the original "Customize World" menu
    // (MainCode.cs, F0_11a8_0486_LogoAndMainGameMenu case 3), with LandMass
    // extended beyond the original 0-2 range: MapInitAndIntro.cs computes
    // totalCellCount = LandMass * 320 + 640, a plain linear formula with no
    // array indexing or hardcoded clamp, so -1 (Tiny) and 3 (Huge) are safe.
    //   Difficulty: 0=Chieftain 1=Warlord 2=Prince 3=King 4=Emperor
    //   LandMass:   -1=Tiny 0=Small 1=Normal 2=Large 3=Huge
    //   Age:        0=3 billion years 1=4 billion years 2=5 billion years
    //   Earth:      loads the original game's real-world MAP.PIC instead of
    //               procedural generation (MainCode.cs case 2, "EARTH").
    //   CustomMap:  name of a map under server/Maps/*.json (see CustomMapFormat.cs)
    //               to load instead of procedural generation. Takes priority over
    //               Earth if both are set. LandMass/Age/Barbarians still apply
    //               (barbarians and difficulty aren't tied to map origin).
    public record NewGameOptions(int Difficulty = 2, int LandMass = 2, int Age = 1, bool Barbarians = false, bool Earth = false, string? CustomMap = null)
    {
        public static NewGameOptions Clamp(NewGameOptions o) => new(
            Math.Clamp(o.Difficulty, 0, 4),
            Math.Clamp(o.LandMass, -1, 3),
            Math.Clamp(o.Age, 0, 2),
            o.Barbarians,
            o.Earth,
            string.IsNullOrWhiteSpace(o.CustomMap) ? null : o.CustomMap);
    }
}
