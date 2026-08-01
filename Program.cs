using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Graphics;
using SlimeDungeon.Guild;
using SlimeDungeon.UI;

if (!SDL.Init(SDL.InitFlags.Video))
{
    Console.Error.WriteLine($"SDL.Init failed: {SDL.GetError()}");
    return 1;
}

if (!SDL.CreateWindowAndRenderer("Slime Dungeon", 640, 400, SDL.WindowFlags.Resizable, out var window, out var rendererHandle))
{
    Console.Error.WriteLine($"CreateWindowAndRenderer failed: {SDL.GetError()}");
    SDL.Quit();
    return 1;
}

SDL.SetRenderLogicalPresentation(rendererHandle, 640, 400, SDL.RendererLogicalPresentation.Letterbox);

var renderer = new Renderer(rendererHandle);
using var fonts = new FontService();
using var sprites = SpriteFactory.BuildAll(rendererHandle);
using var audio = new AudioService();
var input = new InputManager();
var screens = new ScreenManager();

var ctx = new GameContext
{
    Renderer = renderer,
    Input = input,
    Fonts = fonts,
    Screens = screens,
    Sprites = sprites,
    Audio = audio,
    Window = window,
};

screens.ChangeTo(new TitleScreen());
screens.ApplyPendingTransition(ctx);

var inventoryOverlay = new InventoryOverlay();
var killLogOverlay = new KillLogOverlay();

var lastTicks = SDL.GetTicks();
while (!input.QuitRequested)
{
    input.BeginFrame();

    while (SDL.PollEvent(out var ev))
        input.HandleEvent(ev);

    var nowTicks = SDL.GetTicks();
    var dt = (nowTicks - lastTicks) / 1000f;
    lastTicks = nowTicks;

    // Text is rasterised at the window's real pixel size rather than at the 640x400 logical size, so the
    // font service needs to know how far the window is currently being stretched.
    fonts.RefreshPixelScale(rendererHandle);

    screens.ApplyPendingTransition(ctx);

    // Which track plays is decided here from whichever screen is up, rather than by each screen announcing
    // itself. The guild has half a dozen counters hanging off it and the dungeon hands off to combat and
    // back; deriving it centrally means every one of those is covered, and moving between them is seamless
    // because asking for the track already playing does nothing.
    audio.PlayMusic(MusicForScreen(screens.Current));
    audio.UpdateMusic();

    // Snapshot overlay state from *before* this frame's key presses are applied, so the same
    // WasPressed(I)/WasPressed(S) edge that opens an overlay can't also reach its close-check below.
    var overlayActiveBeforeInput = ctx.ShowInventory || ctx.ShowKillLog;
    if (!overlayActiveBeforeInput && ctx.Player is not null)
    {
        if (input.WasPressed(SDL.Keycode.I)) ctx.ShowInventory = true;
        else if (input.WasPressed(SDL.Keycode.S)) ctx.ShowKillLog = true;
    }

    if (overlayActiveBeforeInput && ctx.ShowInventory)
        inventoryOverlay.Update(ctx, dt);
    else if (overlayActiveBeforeInput && ctx.ShowKillLog)
        killLogOverlay.Update(ctx, dt);
    else if (!overlayActiveBeforeInput && !ctx.ShowInventory && !ctx.ShowKillLog)
        screens.Current.Update(ctx, dt);

    screens.Current.Draw(ctx);
    if (ctx.ShowInventory)
        inventoryOverlay.Draw(ctx);
    else if (ctx.ShowKillLog)
        killLogOverlay.Draw(ctx);

    renderer.Present();
}

static MusicId? MusicForScreen(IScreen screen) => screen switch
{
    TitleScreen => MusicId.Title,
    // Combat happens inside a dungeon trip, so the dungeon's music carries straight through it — cutting to
    // silence for every slime would be far more jarring than letting it run under the fight.
    SlimeDungeon.Dungeon.DungeonScreen or SlimeDungeon.Combat.CombatScreen => MusicId.Dungeon,
    // Silence when a character has died. Anything cheerful over that screen would be grotesque.
    GameOverScreen => null,
    // Everything else is the guild and its counters.
    _ => MusicId.Guild,
};

SDL.StopTextInput(window);
SDL.DestroyRenderer(rendererHandle);
SDL.DestroyWindow(window);
SDL.Quit();
return 0;
