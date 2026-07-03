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
    public record NewGameOptions(int Difficulty = 2, int LandMass = 2, int Age = 1, bool Barbarians = false)
    {
        public static NewGameOptions Clamp(NewGameOptions o) => new(
            Math.Clamp(o.Difficulty, 0, 4),
            Math.Clamp(o.LandMass, -1, 3),
            Math.Clamp(o.Age, 0, 2),
            o.Barbarians);
    }
}
