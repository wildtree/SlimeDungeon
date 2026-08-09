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

// Textures were given a blend mode when they were baked, but the renderer's own draw blend mode never was, so
// every FillRect with an alpha in it has been painting solid. That silently disabled things the code plainly
// meant to do — most visibly the dungeon's fog, where explored-but-unseen tiles are covered at alpha 150 and
// so were coming out flat black, making remembered ground indistinguishable from ground never walked.
SDL.SetRenderDrawBlendMode(rendererHandle, SDL.BlendMode.Blend);

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

var itemOverlay = new ItemOverlay();
var equipmentOverlay = new InventoryOverlay();
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

    // Draining the queue is what makes SDL refresh its gamepad state, so the sticks have to be read after it
    // and not before, or every reading would be a frame out of date. It takes dt because a direction held
    // down repeats on a clock.
    input.SampleSticks(dt);

    // Text is rasterised at the window's real pixel size rather than at the 640x400 logical size, so the
    // font service needs to know how far the window is currently being stretched.
    fonts.RefreshPixelScale(rendererHandle);

    screens.ApplyPendingTransition(ctx);

    // Which track plays is decided here from whichever screen is up, rather than by each screen announcing
    // itself. The guild has half a dozen counters hanging off it and the dungeon hands off to combat and
    // back; deriving it centrally means every one of those is covered, and moving between them is seamless
    // because asking for the track already playing does nothing.
    var (wantedMusic, musicDelay, loopMusic) = MusicForScreen(screens.Current);
    audio.PlayMusic(wantedMusic, musicDelay, loopMusic);
    audio.UpdateMusic(dt);

    // Snapshot overlay state from *before* this frame's key presses are applied, so the same keypress that
    // opens an overlay from a screen's menu can't also reach that overlay's close-check below. The overlays
    // are no longer bound to their own hotkeys — they are entries on the menu now, which is what freed S to
    // be the menu button itself.
    var overlayActiveBeforeInput = ctx.AnyOverlayOpen;

    if (overlayActiveBeforeInput && ctx.ShowItems)
        itemOverlay.Update(ctx, dt);
    else if (overlayActiveBeforeInput && ctx.ShowEquipment)
        equipmentOverlay.Update(ctx, dt);
    else if (overlayActiveBeforeInput && ctx.ShowKillLog)
        killLogOverlay.Update(ctx, dt);
    else if (!overlayActiveBeforeInput && !ctx.AnyOverlayOpen)
        screens.Current.Update(ctx, dt);

    screens.Current.Draw(ctx);
    if (ctx.ShowItems)
        itemOverlay.Draw(ctx);
    else if (ctx.ShowEquipment)
        equipmentOverlay.Draw(ctx);
    else if (ctx.ShowKillLog)
        killLogOverlay.Draw(ctx);

    renderer.Present();
}

// One last write on the way out, so closing the window in town is a way of stopping rather than a way of losing
// an afternoon. Until this existed the only autosaves were arriving at the guild, sleeping, buying a display
// case and climbing the stairs out of a dungeon — which meant a session spent shopping and forging had to be
// finished with a pointless dungeon trip to commit any of it.
//
// Two cases are deliberately left unwritten.
//
// A dead character, because GameOverScreen archives them to the history and deletes the active save the moment
// they fall, but ctx.Player stays set until the epitaph is dismissed. Without the guard, quitting while the
// headstone was on screen would write the corpse back over the save it had just been removed from, and the
// adventurer would be sitting in the guild on 0 HP with their own grave already filed.
//
// And anything underground. A trip is committed by the stairs, not by the clock: the save taken on the way in
// is what a quit inside a dungeon falls back to. That does mean closing the window is a way out of a fight
// going badly, which is a real hole — but the alternative hole was worse, because saving on exit anywhere let
// a player walk in, empty the chests, quit, and keep the haul without ever spending the day.
if (ctx.Player is { } departing && !departing.Stats.IsDead && !IsUnderground(screens.Current))
    SlimeDungeon.Data.SaveManager.Save(departing);

/// <summary>
/// Whether the character is out on a trip rather than in town. Combat counts: a fight only ever happens inside
/// a dungeon, and quitting from one has to fall back to the same place the dungeon floor does.
/// </summary>
static bool IsUnderground(IScreen screen) =>
    screen is SlimeDungeon.Dungeon.DungeonScreen or SlimeDungeon.Combat.CombatScreen;

static (MusicId? Track, float Delay, bool Loop) MusicForScreen(IScreen screen) => screen switch
{
    TitleScreen => (MusicId.Title, 0f, true),
    // Registration is the one moment that is purely about what is ahead, so it gets its own theme rather
    // than the guild's everyday one.
    NamingScreen => (MusicId.Registration, 0f, true),
    // Held back so the encounter sting is heard on its own; the dungeon theme keeps running underneath it
    // until the battle music takes over.
    SlimeDungeon.Combat.CombatScreen => (MusicId.Battle, 2.9f, true),
    SlimeDungeon.Dungeon.DungeonScreen => (MusicId.Dungeon, 0f, true),
    // The entrance belongs to the dungeon, not to the town: the guild's cheerful afternoon theme over a
    // ruined arch full of bones was the one place in the game where the music argued with the picture.
    DungeonSelectScreen => (MusicId.Dungeon, 0f, true),
    // The grave gets a lament, and only the one time through. No delay: the battle theme is still running
    // when the player dismisses the last of the fight, and holding the change would leave it playing over
    // the headstone. It is not looped either — a piece that keeps starting again is one the player stops
    // hearing, and this screen is meant to be sat with in the quiet once it has finished.
    GameOverScreen => (MusicId.Requiem, 0f, false),
    // Everything else is the guild and its counters.
    _ => (MusicId.Guild, 0f, true),
};

SDL.StopTextInput(window);
SDL.DestroyRenderer(rendererHandle);
SDL.DestroyWindow(window);
SDL.Quit();
return 0;
