namespace OpenCivOne.Server
{
    public enum ActionType
    {
        MoveUnit,
        FoundCity,
        SetProduction,
        EndTurn,
        SetResearch,
        TextInput,
        KeyPress,
    }

    public record PlayerAction(ActionType Type, int? IntParam = null, string? StringParam = null);
}
