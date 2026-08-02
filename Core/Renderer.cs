using SDL3;

namespace SlimeDungeon.Core;

/// <summary>Thin draw-call wrapper around an SDL renderer handle.</summary>
public sealed class Renderer
{
    public IntPtr Handle { get; }

    public Renderer(IntPtr handle) => Handle = handle;

    public void Clear(SDL.Color color)
    {
        SDL.SetRenderDrawColor(Handle, color.R, color.G, color.B, color.A);
        SDL.RenderClear(Handle);
    }

    public void FillRect(float x, float y, float w, float h, SDL.Color color)
    {
        SDL.SetRenderDrawColor(Handle, color.R, color.G, color.B, color.A);
        var rect = new SDL.FRect { X = x, Y = y, W = w, H = h };
        SDL.RenderFillRect(Handle, rect);
    }

    public void DrawRect(float x, float y, float w, float h, SDL.Color color)
    {
        SDL.SetRenderDrawColor(Handle, color.R, color.G, color.B, color.A);
        var rect = new SDL.FRect { X = x, Y = y, W = w, H = h };
        SDL.RenderRect(Handle, rect);
    }

    public void DrawTexture(IntPtr texture, float x, float y, float w, float h)
    {
        var dst = new SDL.FRect { X = x, Y = y, W = w, H = h };
        SDL.RenderTexture(Handle, texture, IntPtr.Zero, dst);
    }

    /// <summary>
    /// Draws a texture multiplied by <paramref name="tint"/>. Used by the control-hint icons, which are drawn
    /// white once and coloured to match whatever text they sit beside. The modulation is put back afterwards
    /// so the next draw of the same texture is unaffected — these are shared, cached textures.
    /// </summary>
    public void DrawTextureTinted(IntPtr texture, float x, float y, float w, float h, SDL.Color tint)
    {
        SDL.SetTextureColorMod(texture, tint.R, tint.G, tint.B);
        SDL.SetTextureAlphaMod(texture, tint.A);
        DrawTexture(texture, x, y, w, h);
        SDL.SetTextureColorMod(texture, 255, 255, 255);
        SDL.SetTextureAlphaMod(texture, 255);
    }

    public void Present() => SDL.RenderPresent(Handle);
}

public static class Colors
{
    public static SDL.Color Rgb(byte r, byte g, byte b, byte a = 255) => new() { R = r, G = g, B = b, A = a };

    public static readonly SDL.Color Black = Rgb(0, 0, 0);
    public static readonly SDL.Color White = Rgb(255, 255, 255);
    public static readonly SDL.Color DungeonBg = Rgb(18, 18, 24);
    public static readonly SDL.Color PanelBg = Rgb(30, 28, 40);
    public static readonly SDL.Color Border = Rgb(90, 80, 70);
    public static readonly SDL.Color HpBar = Rgb(200, 40, 50);
    public static readonly SDL.Color MpBar = Rgb(50, 90, 220);
    public static readonly SDL.Color ExpBar = Rgb(230, 200, 40);
    public static readonly SDL.Color BarBg = Rgb(50, 50, 55);
    public static readonly SDL.Color Gold = Rgb(230, 200, 60);
    public static readonly SDL.Color Highlight = Rgb(255, 230, 120);
}
