namespace SlimeDungeon.Core;

public enum SoundId
{
    WeaponHit,
    MagicAttack,
    MagicHeal,
    LevelUpFanfare,
    RankUpFanfare,
    TitleFanfare,
}

/// <summary>
/// Every sound in the game, synthesised at startup. There are no audio files for the same reason there are no
/// image files: the whole game is generated from code, and a handful of oscillators and envelopes is enough for
/// the six sounds it needs. The three fanfares are written here as note lists — original melodies, deliberately
/// so; they are meant to sit in the same idiom as the jingles everyone knows without borrowing from any of them.
/// </summary>
public static class SoundBank
{
    public const int SampleRate = 44100;

    private enum Wave { Sine, Square, Saw, Triangle, Noise, Brass, Bell }

    /// <summary>One note of a written-out phrase: when it starts, how long it rings, and how loud.</summary>
    private readonly record struct Note(double Start, double Duration, int Midi, double Amp = 1.0);

    public static IReadOnlyDictionary<SoundId, short[]> BuildAll() => new Dictionary<SoundId, short[]>
    {
        [SoundId.WeaponHit] = WeaponHit(),
        [SoundId.MagicAttack] = MagicAttack(),
        [SoundId.MagicHeal] = MagicHeal(),
        [SoundId.LevelUpFanfare] = LevelUpFanfare(),
        [SoundId.RankUpFanfare] = RankUpFanfare(),
        [SoundId.TitleFanfare] = TitleFanfare(),
    };

    // ---- Combat effects ----------------------------------------------------------------------

    /// <summary>
    /// A blade landing: a noise transient for the bite of it over a fast downward tone for the weight. Short
    /// and dry, because it fires once per attack and anything with a tail would smear across the turn.
    /// </summary>
    private static short[] WeaponHit()
    {
        var buf = NewBuffer(0.22);
        AddNoise(buf, 0.0, 0.13, amp: 0.55, decay: 34);
        AddSweep(buf, 0.0, 0.10, fromHz: 1500, toHz: 260, Wave.Square, amp: 0.34, decay: 26);
        AddSweep(buf, 0.0, 0.16, fromHz: 220, toHz: 90, Wave.Sine, amp: 0.30, decay: 16);
        return Finish(buf, peak: 0.55);
    }

    /// <summary>
    /// Magic: tonal where the blade is noisy, so the two are never mistaken for one another. A rising charge
    /// snapping into a falling discharge, with a detuned twin an eyelash off pitch to make it shimmer.
    /// </summary>
    private static short[] MagicAttack()
    {
        var buf = NewBuffer(0.52);
        AddSweep(buf, 0.00, 0.14, fromHz: 240, toHz: 1400, Wave.Saw, amp: 0.26, decay: 0);
        AddSweep(buf, 0.14, 0.34, fromHz: 1400, toHz: 320, Wave.Saw, amp: 0.34, decay: 7);
        AddSweep(buf, 0.14, 0.34, fromHz: 1410, toHz: 322, Wave.Sine, amp: 0.22, decay: 7);
        AddSweep(buf, 0.02, 0.30, fromHz: 3000, toHz: 1800, Wave.Sine, amp: 0.10, decay: 9);
        AddNoise(buf, 0.0, 0.10, amp: 0.12, decay: 30);
        return Finish(buf, peak: 0.5);
    }

    /// <summary>Healing: a major triad rolled upward on soft bells — warm, unhurried, obviously not a hit.</summary>
    private static short[] MagicHeal()
    {
        var buf = NewBuffer(0.95);
        AddNote(buf, 0.00, 0.75, Midi(72), Wave.Bell, 0.30);   // C5
        AddNote(buf, 0.09, 0.70, Midi(76), Wave.Bell, 0.27);   // E5
        AddNote(buf, 0.18, 0.68, Midi(79), Wave.Bell, 0.27);   // G5
        AddNote(buf, 0.30, 0.55, Midi(84), Wave.Bell, 0.18);   // C6 sparkle
        return Finish(buf, peak: 0.5);
    }

    // ---- Fanfares ----------------------------------------------------------------------------

    /// <summary>
    /// Level up. Bright and ascending: a rise through the tonic chord, a lift over the fourth and fifth, then
    /// a held octave to land on. Roughly two and a half seconds — long enough to feel like an occasion,
    /// short enough that it never outstays a routine kill.
    /// </summary>
    private static short[] LevelUpFanfare()
    {
        Note[] lead =
        [
            new(0.00, 0.12, 67), new(0.12, 0.12, 72), new(0.24, 0.12, 76), new(0.36, 0.26, 79),
            new(0.62, 0.12, 77), new(0.74, 0.12, 79), new(0.86, 0.28, 81),
            new(1.14, 0.12, 79), new(1.26, 0.12, 76), new(1.38, 0.95, 84),
        ];
        Note[] harmony =
        [
            new(0.36, 0.26, 72, 0.5), new(0.86, 0.28, 77, 0.5), new(1.38, 0.95, 79, 0.55),
        ];
        Note[] bass =
        [
            new(0.00, 0.34, 48), new(0.36, 0.24, 55), new(0.62, 0.50, 53),
            new(1.14, 0.22, 55), new(1.38, 0.95, 48),
        ];
        return Render(2.75, lead, harmony, bass);
    }

    /// <summary>
    /// Promotion. More ceremony than celebration: a dotted call on the tonic, a stepwise climb, and a broad
    /// held finish with the chord filled in underneath, the way a herald's announcement would sit.
    /// </summary>
    private static short[] RankUpFanfare()
    {
        Note[] lead =
        [
            new(0.00, 0.22, 72), new(0.24, 0.10, 72), new(0.34, 0.32, 77),
            new(0.66, 0.14, 81), new(0.80, 0.14, 79), new(0.94, 0.14, 77), new(1.08, 0.14, 79),
            new(1.22, 0.34, 81), new(1.56, 0.16, 79), new(1.72, 1.00, 84),
        ];
        Note[] harmony =
        [
            new(0.34, 0.32, 72, 0.5), new(1.22, 0.34, 77, 0.5),
            new(1.72, 1.00, 77, 0.55), new(1.72, 1.00, 81, 0.4),
        ];
        Note[] bass =
        [
            new(0.00, 0.32, 53), new(0.34, 0.30, 41), new(0.66, 0.54, 48),
            new(1.22, 0.32, 41), new(1.72, 1.00, 53),
        ];
        return Render(2.85, lead, harmony, bass);
    }

    /// <summary>
    /// A title being conferred. Quieter and a shade more solemn than the other two — bells rather than brass,
    /// a phrase that turns downward before it rises, resolving onto an open chord. An honour, not a victory.
    /// </summary>
    private static short[] TitleFanfare()
    {
        Note[] lead =
        [
            new(0.00, 0.20, 69), new(0.20, 0.20, 74), new(0.40, 0.34, 77),
            new(0.74, 0.16, 76), new(0.90, 0.16, 74), new(1.06, 0.42, 81),
            new(1.48, 0.20, 79), new(1.68, 1.05, 77),
        ];
        Note[] harmony =
        [
            new(0.40, 0.34, 69, 0.45), new(1.06, 0.42, 74, 0.45),
            new(1.68, 1.05, 81, 0.5), new(1.68, 1.05, 86, 0.35),
        ];
        Note[] bass =
        [
            new(0.00, 0.38, 50), new(0.40, 0.32, 45), new(0.74, 0.30, 50),
            new(1.06, 0.40, 45), new(1.68, 1.05, 50),
        ];
        return Render(2.90, lead, harmony, bass);
    }

    /// <summary>Lays a three-part phrase down: brass lead, quieter brass harmony, square-wave bass.</summary>
    private static short[] Render(double seconds, Note[] lead, Note[] harmony, Note[] bass)
    {
        var buf = NewBuffer(seconds);
        foreach (var n in lead)
            AddNote(buf, n.Start, n.Duration, Midi(n.Midi), Wave.Brass, 0.34 * n.Amp);
        foreach (var n in harmony)
            AddNote(buf, n.Start, n.Duration, Midi(n.Midi), Wave.Brass, 0.20 * n.Amp);
        foreach (var n in bass)
            AddNote(buf, n.Start, n.Duration, Midi(n.Midi), Wave.Square, 0.16 * n.Amp);
        return Finish(buf, peak: 0.62);
    }

    // ---- Synthesis ---------------------------------------------------------------------------

    private static double Midi(int note) => 440.0 * Math.Pow(2, (note - 69) / 12.0);

    private static float[] NewBuffer(double seconds) => new float[(int)(seconds * SampleRate)];

    /// <summary>
    /// A single sustained note. The envelope is a quick attack, a body that sags slightly (real instruments
    /// never hold perfectly flat), and a release long enough not to click when it stops.
    /// </summary>
    private static void AddNote(float[] buf, double start, double duration, double freq, Wave wave, double amp)
    {
        var from = (int)(start * SampleRate);
        var count = (int)(duration * SampleRate);
        const double attack = 0.012;
        var release = Math.Min(0.10, duration * 0.4);

        for (var i = 0; i < count; i++)
        {
            var idx = from + i;
            if (idx < 0 || idx >= buf.Length)
                continue;

            var t = i / (double)SampleRate;
            var env = Envelope(t, duration, attack, release);
            if (wave == Wave.Bell)
                env *= Math.Exp(-t * 3.2);
            buf[idx] += (float)(Sample(wave, freq, t) * env * amp);
        }
    }

    /// <summary>A tone whose pitch slides from one frequency to another — the backbone of both combat sounds.</summary>
    private static void AddSweep(float[] buf, double start, double duration, double fromHz, double toHz,
        Wave wave, double amp, double decay)
    {
        var from = (int)(start * SampleRate);
        var count = (int)(duration * SampleRate);
        var phase = 0.0;

        for (var i = 0; i < count; i++)
        {
            var idx = from + i;
            if (idx < 0 || idx >= buf.Length)
                continue;

            var t = i / (double)SampleRate;
            var progress = count <= 1 ? 0 : i / (double)(count - 1);
            var freq = fromHz + (toHz - fromHz) * progress;
            phase += freq / SampleRate;

            var env = decay > 0 ? Math.Exp(-t * decay) : Envelope(t, duration, 0.008, 0.03);
            buf[idx] += (float)(Shape(wave, phase) * env * amp);
        }
    }

    private static void AddNoise(float[] buf, double start, double duration, double amp, double decay)
    {
        var from = (int)(start * SampleRate);
        var count = (int)(duration * SampleRate);
        var rnd = new Random(20260731);

        // A touch of smoothing takes the hiss off raw white noise and leaves something closer to a struck edge.
        var previous = 0.0;
        for (var i = 0; i < count; i++)
        {
            var idx = from + i;
            if (idx < 0 || idx >= buf.Length)
                continue;

            var t = i / (double)SampleRate;
            var white = rnd.NextDouble() * 2 - 1;
            previous = previous * 0.45 + white * 0.55;
            buf[idx] += (float)(previous * Math.Exp(-t * decay) * amp);
        }
    }

    private static double Envelope(double t, double duration, double attack, double release)
    {
        if (t < attack)
            return t / attack;
        var untilEnd = duration - t;
        if (untilEnd < release)
            return Math.Max(0, untilEnd / release);
        // A gentle sag across the body rather than a dead-flat sustain.
        return 1.0 - 0.25 * ((t - attack) / Math.Max(1e-6, duration - attack));
    }

    private static double Sample(Wave wave, double freq, double t) => Shape(wave, freq * t);

    /// <summary>
    /// Waveform from a running phase in turns. Brass and bell are additive rather than naive saw/square
    /// shapes, which keeps them from aliasing into a fizz at the top of the fanfares.
    /// </summary>
    private static double Shape(Wave wave, double phase)
    {
        var w = phase * 2 * Math.PI;
        switch (wave)
        {
            case Wave.Sine:
                return Math.Sin(w);
            case Wave.Square:
                return Math.Sin(w) >= 0 ? 0.7 : -0.7;
            case Wave.Saw:
                return 2 * (phase - Math.Floor(phase + 0.5));
            case Wave.Triangle:
                return 2 * Math.Abs(2 * (phase - Math.Floor(phase + 0.5))) - 1;
            case Wave.Brass:
                return 0.58 * Math.Sin(w)
                     + 0.30 * Math.Sin(2 * w)
                     + 0.16 * Math.Sin(3 * w)
                     + 0.08 * Math.Sin(4 * w)
                     + 0.04 * Math.Sin(5 * w);
            case Wave.Bell:
                return 0.62 * Math.Sin(w)
                     + 0.24 * Math.Sin(2.76 * w)
                     + 0.12 * Math.Sin(5.40 * w);
            default:
                return 0;
        }
    }

    /// <summary>Normalises to a fixed headroom and converts to the 16-bit samples the device is opened with.</summary>
    private static short[] Finish(float[] buf, double peak)
    {
        var max = 0.0;
        foreach (var v in buf)
            max = Math.Max(max, Math.Abs(v));
        var gain = max > 1e-6 ? peak / max : 0;

        var output = new short[buf.Length];
        for (var i = 0; i < buf.Length; i++)
            output[i] = (short)Math.Clamp(buf[i] * gain * short.MaxValue, short.MinValue, short.MaxValue);
        return output;
    }
}
