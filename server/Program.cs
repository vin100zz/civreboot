using System.Text.Json;
using OpenCivOne.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GameServer>();

var app = builder.Build();

// Eagerly start the game at server launch (not on first request)
app.Services.GetRequiredService<GameServer>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/state", (GameServer gs) =>
    Results.Text(gs.GetState(), "application/json"));

app.MapPost("/api/action", async (GameServer gs, HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;

    var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
    int? intParam = root.TryGetProperty("param", out var p) ? p.GetInt32() : null;
    string? strParam = root.TryGetProperty("text", out var s) ? s.GetString() : null;

    ActionType actionType = type switch
    {
        "move"    => ActionType.MoveUnit,
        "found"   => ActionType.FoundCity,
        "endturn" => ActionType.EndTurn,
        "key"     => ActionType.KeyPress,
        "text"    => ActionType.TextInput,
        _         => ActionType.KeyPress,
    };

    var state = gs.InjectAndWait(new PlayerAction(actionType, intParam, strParam));
    return Results.Text(state, "application/json");
});

app.MapPost("/api/reveal", (GameServer gs) =>
    Results.Text(gs.RevealMap(), "application/json"));

app.Run();
