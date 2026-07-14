using System.Text.Json;
using OpenCivOne;
using OpenCivOne.Server;

// Client lives at reboot2/client, a sibling of this project's directory
// (not "wwwroot") — resolved relative to the content root, same as ASP.NET's
// default wwwroot convention would be.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "../client",
});
builder.Services.AddSingleton<GameServer>();

var app = builder.Build();

// Eagerly start the game at server launch (not on first request)
app.Services.GetRequiredService<GameServer>();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // This is a dev prototype iterated on constantly — always revalidate so
    // browsers don't serve a stale cached copy of the JS/CSS after an edit.
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

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

app.MapPost("/api/viewmode", async (GameServer gs, HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    using var doc = JsonDocument.Parse(body);
    int mode = doc.RootElement.TryGetProperty("mode", out var m) ? m.GetInt32() : -1;
    return Results.Text(gs.SetViewMode(mode), "application/json");
});

app.MapPost("/api/newgame", async (GameServer gs, HttpContext ctx) =>
{
    NewGameOptions? options = null;
    using (var reader = new StreamReader(ctx.Request.Body))
    {
        var body = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            options = new NewGameOptions(
                Difficulty: root.TryGetProperty("difficulty", out var d) ? d.GetInt32() : 2,
                LandMass: root.TryGetProperty("landMass", out var l) ? l.GetInt32() : 2,
                Age: root.TryGetProperty("age", out var a) ? a.GetInt32() : 1,
                Barbarians: root.TryGetProperty("barbarians", out var b) && b.GetBoolean(),
                Earth: root.TryGetProperty("earth", out var e) && e.GetBoolean(),
                CustomMap: root.TryGetProperty("customMap", out var m) ? m.GetString() : null);
        }
    }
    return Results.Text(gs.NewGame(options), "application/json");
});

app.MapGet("/api/maps", () =>
    Results.Json(OpenCivOne.Server.Maps.CustomMapFormat.ListMaps()));

app.Run();
