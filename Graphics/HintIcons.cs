using SDL3;
using SlimeDungeon.Core;

namespace SlimeDungeon.Graphics;

/// <summary>The controls a hint line can name.</summary>
public enum HintIcon { Direction, Confirm, Cancel, Menu, Travel }

/// <summary>
/// The little glyphs the control hints are drawn with.
///
/// These are pixel art rather than characters from the UI font, and deliberately so. The game is developed and
/// played across Windows and Linux, where the font actually loaded is whatever CJK face the machine happens to
/// have; symbols like ☰ and ✓ are missing from plenty of them, and a missing glyph renders as a blank or a
/// tofu box with no warning at all. Drawing them ourselves is the only way a hint looks the same everywhere.
///
/// They are drawn white on transparent and tinted at draw time, so one texture serves every colour of hint.
/// </summary>
public static class HintIcons
{
    /// <summary>
    /// Icons are authored at this size and scaled to whatever the surrounding text is. Small enough to stay
    /// crisp, large enough that the arrow heads and the tick have room to read as shapes.
    /// </summary>
    public const int Size = 16;

    private static readonly SDL.Color On = Colors.Rgb(255, 255, 255);

    public static PixelCanvas Build(HintIcon icon) => icon switch
    {
        HintIcon.Direction => BuildDirection(),
        HintIcon.Confirm => BuildConfirm(),
        HintIcon.Cancel => BuildCancel(),
        HintIcon.Travel => BuildTravel(),
        _ => BuildMenu(),
    };

    /// <summary>
    /// Up, down, left and right as one pointed cross — the shape of a d-pad.
    ///
    /// Two earlier attempts drew four separate arrow heads. At the size this is actually shown, roughly twelve
    /// pixels, thin heads dissolved into a smudge, and fattening them until they held together made them touch
    /// and merge into a plain diamond. Sixteen pixels is not enough room for four distinct arrows that are also
    /// solid, so this is one connected glyph instead: a thick cross with a point on each arm, which survives
    /// the downscale and still says "all four directions" at a glance.
    /// </summary>
    private static PixelCanvas BuildDirection()
    {
        var c = new PixelCanvas(Size, Size);

        // The arms.
        c.FillRect(6, 3, 4, 10, On);
        c.FillRect(3, 6, 10, 4, On);

        // A point on the end of each, one step narrower than the arm it caps.
        c.FillRect(7, 1, 2, 2, On);
        c.FillRect(7, 13, 2, 2, On);
        c.FillRect(1, 7, 2, 2, On);
        c.FillRect(13, 7, 2, 2, On);

        return c;
    }

    /// <summary>A tick. Short stroke down into the corner, long stroke up and out to the right.</summary>
    private static PixelCanvas BuildConfirm()
    {
        var c = new PixelCanvas(Size, Size);

        // Down-right from the left, meeting the bottom of the long stroke.
        for (var i = 0; i < 4; i++)
            c.FillRect(2 + i, 7 + i, 2, 2, On);

        // Up-right, twice as long, so the tick is lopsided the way a handwritten one is.
        for (var i = 0; i < 7; i++)
            c.FillRect(6 + i, 10 - i, 2, 2, On);

        return c;
    }

    /// <summary>
    /// A dash. Cancel is the one control with no natural symbol, and a cross would read as "wrong" rather than
    /// "back" — a dash is the neutral mark the player asked for.
    /// </summary>
    private static PixelCanvas BuildCancel()
    {
        var c = new PixelCanvas(Size, Size);
        c.FillRect(2, 7, 12, 2, On);
        return c;
    }

    /// <summary>
    /// A map pin: a ring on a tapering point. Chosen over an arrow because the direction cross already owns
    /// arrowheads in this set, and over footprints because two small marks turn to mush at this size. The hole
    /// in the middle is what stops it reading as a blob — it is the only glyph here that is not a solid shape,
    /// which also makes it the easiest of the five to pick out of a hint line.
    /// </summary>
    private static PixelCanvas BuildTravel()
    {
        var c = new PixelCanvas(Size, Size);

        // The head, drawn as a stack of rows so the ring is round rather than square.
        c.FillRect(5, 1, 6, 1, On);
        c.FillRect(4, 2, 8, 1, On);
        c.FillRect(3, 3, 10, 1, On);
        for (var y = 4; y <= 6; y++)
        {
            c.FillRect(3, y, 3, 1, On);
            c.FillRect(10, y, 3, 1, On);
        }
        c.FillRect(3, 7, 10, 1, On);
        c.FillRect(4, 8, 8, 1, On);

        // The point, narrowing to a single pixel at the bottom.
        c.FillRect(5, 9, 6, 1, On);
        c.FillRect(6, 10, 4, 1, On);
        c.FillRect(6, 11, 4, 1, On);
        c.FillRect(7, 12, 2, 1, On);
        c.FillRect(7, 13, 2, 1, On);

        return c;
    }

    /// <summary>Three stacked bars: the hamburger every menu button in the world is drawn as.</summary>
    private static PixelCanvas BuildMenu()
    {
        var c = new PixelCanvas(Size, Size);
        c.FillRect(2, 3, 12, 2, On);
        c.FillRect(2, 7, 12, 2, On);
        c.FillRect(2, 11, 12, 2, On);
        return c;
    }
}
