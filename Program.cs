using SDL3;
using SlimeDungeon.Core;
using SlimeDungeon.Graphics;
using SlimeDungeon.Guild;
using SlimeDungeon.UI;

// Gamepad has to be asked for explicitly: without its subsystem running, SDL never opens a controller and
// never sends a single button event, so the pad bindings looked correct and did nothing at all.
if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Gamepad))
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
    var (wantedMusic, musicDelay) = MusicForScreen(screens.Current);
    audio.PlayMusic(wantedMusic, musicDelay);
    audio.UpdateMusic(dt);

    // Snapshot overlay state from *before* this frame's key presses are applied, so the same keypress that
    // opens an overlay from a screen's menu can't also reach that overlay's close-check below. The overlays
    // are no longer bound to their own hotkeys — they are entries on the menu now, which is what freed S to
    // be the menu button itself.
    var overlayActiveBeforeInput = ctx.ShowInventory || ctx.ShowKillLog;

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

static (MusicId? Track, float Delay) MusicForScreen(IScreen screen) => screen switch
{
    TitleScreen => (MusicId.Title, 0f),
    // Registration is the one moment that is purely about what is ahead, so it gets its own theme rather
    // than the guild's everyday one.
    NamingScreen => (MusicId.Registration, 0f),
    // Held back so the encounter sting is heard on its own; the dungeon theme keeps running underneath it
    // until the battle music takes over.
    SlimeDungeon.Combat.CombatScreen => (MusicId.Battle, 2.9f),
    SlimeDungeon.Dungeon.DungeonScreen => (MusicId.Dungeon, 0f),
    // Silence when a character has died. Anything cheerful over that screen would be grotesque.
    GameOverScreen => (null, 0f),
    // Everything else is the guild and its counters.
    _ => (MusicId.Guild, 0f),
};

SDL.StopTextInput(window);
SDL.DestroyRenderer(rendererHandle);
SDL.DestroyWindow(window);
SDL.Quit();
return 0;
