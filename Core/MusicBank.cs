namespace SlimeDungeon.Core;

public enum MusicId { Title, Guild, Dungeon, Battle, Registration }

/// <summary>
/// The three background pieces, synthesised like everything else in this game rather than loaded from files.
///
/// These are built with actual frequency modulation — a carrier whose phase is pushed around by a second
/// oscillator — because that is what gives the sound its era. Additive synthesis (what the sound effects use)
/// produces clean, glassy tones; FM produces the slightly metallic, hollow, bell-and-reed voices of the
/// four-operator chips these tracks are meant to evoke. A voice here is a ratio between the two oscillators
/// plus a modulation index that falls away as the note decays, which is the whole trick.
/// </summary>
public static class MusicBank
{
    /// <summary>
    /// Half the sample rate of the effects. Music is thirty seconds a track rather than a fraction of a
    /// second, so this halves both the generation time at startup and the memory the loops sit in, and at
    /// these timbres the difference is inaudible. SDL resamples it to whatever the device wants.
    /// </summary>
    public const int SampleRate = 22050;

    /// <summary>
    /// A modulator:carrier ratio, how hard the modulator pushes, and how that push decays. Whole-number
    /// ratios give harmonic, instrument-like tones; the fractional ones give the inharmonic, bell-like voices.
    /// </summary>
    private sealed record Voice(
        double Ratio,
        double Index,
        double IndexDecay,
        double Attack,
        double Release,
        double Amp,
        double Detune = 0);

    private readonly record struct N(double Start, double Duration, int Midi, double Amp = 1.0);

    // ---- Voices ------------------------------------------------------------------------------

    /// <summary>Round and hollow, the classic FM electric piano. Carries most of the melodies.</summary>
    private static readonly Voice EPiano = new(Ratio: 2.0, Index: 3.2, IndexDecay: 4.5, Attack: 0.006, Release: 0.28, Amp: 0.30);

    /// <summary>A reedy horn: the modulation swells rather than decays, so the tone opens as it sounds.</summary>
    private static readonly Voice Horn = new(Ratio: 1.0, Index: 2.6, IndexDecay: 1.1, Attack: 0.06, Release: 0.30, Amp: 0.26);

    /// <summary>Soft sustained backing. Very little modulation, so it sits under everything without crowding.</summary>
    private static readonly Voice Pad = new(Ratio: 1.0, Index: 0.9, IndexDecay: 0.5, Attack: 0.35, Release: 0.7, Amp: 0.16);

    /// <summary>Short, woody, percussive bass.</summary>
    private static readonly Voice Bass = new(Ratio: 1.0, Index: 2.2, IndexDecay: 7.0, Attack: 0.004, Release: 0.10, Amp: 0.34);

    /// <summary>Inharmonic ratio: a struck bell rather than a plucked string.</summary>
    private static readonly Voice Bell = new(Ratio: 3.5, Index: 4.0, IndexDecay: 3.0, Attack: 0.003, Release: 0.5, Amp: 0.20);

    /// <summary>Detuned and barely modulated — an uneasy, wavering sustain for the dungeon.</summary>
    private static readonly Voice Drone = new(Ratio: 1.0, Index: 1.4, IndexDecay: 0.25, Attack: 0.6, Release: 1.2, Amp: 0.18, Detune: 0.7);

    public static IReadOnlyDictionary<MusicId, short[]> BuildAll() => new Dictionary<MusicId, short[]>
    {
        [MusicId.Title] = Title(),
        [MusicId.Guild] = Guild(),
        [MusicId.Dungeon] = Dungeon(),
        [MusicId.Battle] = Battle(),
        [MusicId.Registration] = Registration(),
    };

    // ---- The pieces --------------------------------------------------------------------------

    private const double Bar = 2.0;

    /// <summary>
    /// Title: broad and unhurried. Long horn notes over sustained chords, moving in whole bars so nothing
    /// feels hurried, and a bass that walks rather than pulses. Fifteen bars, ending on the dominant so the
    /// loop pulls back round to the opening chord instead of stopping.
    /// </summary>
    private static short[] Title()
    {
        // C  C  Am Am F  F  G  G  C  C  Em Em F  G  (then round again)
        int[][] chords =
        [
            [60, 64, 67], [60, 64, 67], [57, 60, 64], [57, 60, 64],
            [53, 57, 60], [53, 57, 60], [55, 59, 62], [55, 59, 62],
            [60, 64, 67], [60, 64, 67], [52, 55, 59], [52, 55, 59],
            [53, 57, 60], [55, 59, 62], [55, 59, 62],
        ];

        var pad = new List<N>();
        var bass = new List<N>();
        for (var bar = 0; bar < chords.Length; bar++)
        {
            var t = bar * Bar;
            foreach (var note in chords[bar])
                pad.Add(new N(t, Bar * 0.98, note, 0.85));

            // Root, then the fifth halfway through: a slow stride under the sustained chord.
            bass.Add(new N(t, Bar * 0.45, chords[bar][0] - 24));
            bass.Add(new N(t + Bar * 0.5, Bar * 0.45, chords[bar][2] - 24, 0.8));
        }

        // A horn call in long values. It climbs to the top of its range at bar 8 and settles back.
        List<N> lead =
        [
            new(0.0, 1.6, 67), new(2.0, 1.9, 72), new(4.0, 1.6, 76), new(6.0, 1.9, 74),
            new(8.0, 1.6, 72), new(10.0, 0.8, 69), new(11.0, 0.9, 72), new(12.0, 3.6, 76),
            new(16.0, 1.6, 79), new(18.0, 1.9, 76), new(20.0, 1.6, 71), new(22.0, 1.9, 74),
            new(24.0, 1.6, 72), new(26.0, 0.8, 69), new(27.0, 0.9, 67), new(28.0, 1.9, 71),
        ];

        // A few bell notes high above, sparse enough to read as decoration rather than a second melody.
        List<N> sparkle =
        [
            new(6.0, 1.0, 88, 0.5), new(14.0, 1.0, 91, 0.45),
            new(22.0, 1.0, 88, 0.5), new(29.0, 1.0, 84, 0.4),
        ];

        return Render(30.0, (Pad, pad), (Bass, bass), (Horn, lead), (Bell, sparkle));
    }

    /// <summary>
    /// Guild: an ordinary pleasant afternoon. Major throughout, a light two-feel in the bass, and a melody
    /// that skips along in short notes without ever going anywhere dramatic. Nothing here resolves upward —
    /// it just keeps turning over, which is the point.
    /// </summary>
    private static short[] Guild()
    {
        // F Bb C F  Dm Bb C F  F Bb C Dm  Bb C F
        int[][] chords =
        [
            [53, 57, 60], [58, 62, 65], [60, 64, 67], [53, 57, 60],
            [50, 53, 57], [58, 62, 65], [60, 64, 67], [53, 57, 60],
            [53, 57, 60], [58, 62, 65], [60, 64, 67], [50, 53, 57],
            [58, 62, 65], [60, 64, 67], [53, 57, 60],
        ];

        var comp = new List<N>();
        var bass = new List<N>();
        for (var bar = 0; bar < chords.Length; bar++)
        {
            var t = bar * Bar;
            var c = chords[bar];

            // Chord on the offbeats, the way a rhythm part would comp behind a tune.
            foreach (var note in c)
            {
                comp.Add(new N(t + Bar * 0.25, Bar * 0.20, note + 12, 0.45));
                comp.Add(new N(t + Bar * 0.75, Bar * 0.20, note + 12, 0.40));
            }

            bass.Add(new N(t, Bar * 0.22, c[0] - 12));
            bass.Add(new N(t + Bar * 0.5, Bar * 0.22, c[0] - 12, 0.75));
        }

        // Eight-bar tune, repeated with a different tail so the loop does not feel like a two-bar cell.
        List<N> lead =
        [
            new(0.0, 0.45, 72), new(0.5, 0.45, 74), new(1.0, 0.9, 77),
            new(2.0, 0.45, 74), new(2.5, 0.45, 77), new(3.0, 0.9, 79),
            new(4.0, 0.45, 76), new(4.5, 0.45, 74), new(5.0, 0.9, 72),
            new(6.0, 1.8, 69),

            new(8.0, 0.45, 69), new(8.5, 0.45, 72), new(9.0, 0.9, 74),
            new(10.0, 0.45, 77), new(10.5, 0.45, 74), new(11.0, 0.9, 72),
            new(12.0, 0.45, 71), new(12.5, 0.45, 72), new(13.0, 0.9, 74),
            new(14.0, 1.8, 72),

            new(16.0, 0.45, 72), new(16.5, 0.45, 74), new(17.0, 0.9, 77),
            new(18.0, 0.45, 79), new(18.5, 0.45, 81), new(19.0, 0.9, 79),
            new(20.0, 0.45, 77), new(20.5, 0.45, 76), new(21.0, 0.9, 74),
            new(22.0, 1.8, 74),

            new(24.0, 0.45, 76), new(24.5, 0.45, 74), new(25.0, 0.9, 72),
            new(26.0, 0.45, 71), new(26.5, 0.45, 69), new(27.0, 0.9, 71),
            new(28.0, 1.9, 72),
        ];

        return Render(30.0, (Bass, bass), (EPiano, comp), (EPiano, lead));
    }

    /// <summary>
    /// Dungeon: not frightening, but not comfortable. A drone on the tonic that never lets go, a minor
    /// progression that keeps sliding to chords a semitone away from where the ear expects, and a melody with
    /// long gaps in it — the unease is mostly in what is missing. The last chord is a tritone away from the
    /// first, so the loop never resolves.
    /// </summary>
    private static short[] Dungeon()
    {
        const double slowBar = 2.5;

        // Dm  Dm  Bb  A   Dm  Gm  Eb  A   Dm  Bb  Gm  A
        int[][] chords =
        [
            [50, 53, 57], [50, 53, 57], [46, 50, 53], [45, 49, 52],
            [50, 53, 57], [55, 58, 62], [51, 55, 58], [45, 49, 52],
            [50, 53, 57], [46, 50, 53], [55, 58, 62], [45, 49, 52],
        ];

        var pad = new List<N>();
        var bass = new List<N>();
        for (var bar = 0; bar < chords.Length; bar++)
        {
            var t = bar * slowBar;
            foreach (var note in chords[bar])
                pad.Add(new N(t, slowBar * 0.95, note, 0.7));

            // A single low note per bar, left to ring — no pulse, nothing to march to.
            bass.Add(new N(t, slowBar * 0.8, chords[bar][0] - 24, 0.9));
        }

        // The drone: D held underneath the whole piece regardless of what the chords do above it. Where the
        // harmony moves away from D, this is what makes it grind.
        List<N> drone =
        [
            new(0.0, 10.0, 38), new(10.0, 10.0, 38), new(20.0, 10.2, 38),
        ];

        // Sparse, hesitant, and it keeps landing on the flattened fifth.
        List<N> lead =
        [
            new(1.2, 0.9, 74), new(2.4, 0.6, 77), new(3.2, 1.4, 76),
            new(7.5, 0.9, 72), new(8.7, 0.6, 74), new(9.6, 1.6, 68),   // A♭: the tritone
            new(14.0, 0.9, 77), new(15.2, 0.6, 79), new(16.1, 1.6, 77),
            new(20.5, 0.8, 74), new(21.6, 0.5, 72), new(22.4, 1.8, 69),
            new(26.5, 0.9, 68), new(27.8, 1.9, 66),
        ];

        // Two isolated bell notes, high and far apart: the sound of something else being down here.
        List<N> bells =
        [
            new(5.5, 1.2, 86, 0.35), new(18.8, 1.2, 89, 0.30),
        ];

        return Render(30.0, (Drone, drone), (Pad, pad), (Bass, bass), (EPiano, lead), (Bell, bells));
    }

    /// <summary>
    /// Battle: fast and driving. A bass in continuous eighths so the pulse never lets up, chord stabs on the
    /// offbeats to push against it, and a melody that runs rather than sings. Minor, but with a lift into the
    /// major chord at the turnaround so it reads as urgent rather than grim — these are slimes, not a demon
    /// lord. Twenty bars at a bar and a half, which is twice the harmonic pace of the dungeon theme.
    /// </summary>
    private static short[] Battle()
    {
        const double bar = 1.5;

        // Dm Dm Bb C  Dm Dm Bb A  Dm F  C  Dm  Bb C  Dm Dm  Bb C  A  A
        int[][] chords =
        [
            [50, 53, 57], [50, 53, 57], [46, 50, 53], [48, 52, 55],
            [50, 53, 57], [50, 53, 57], [46, 50, 53], [45, 49, 52],
            [50, 53, 57], [53, 57, 60], [48, 52, 55], [50, 53, 57],
            [46, 50, 53], [48, 52, 55], [50, 53, 57], [50, 53, 57],
            [46, 50, 53], [48, 52, 55], [45, 49, 52], [45, 49, 52],
        ];

        var bass = new List<N>();
        var stabs = new List<N>();
        for (var barIndex = 0; barIndex < chords.Length; barIndex++)
        {
            var t = barIndex * bar;
            var c = chords[barIndex];
            var eighth = bar / 8;

            // Eight to the bar, alternating root and fifth: the engine of the whole piece.
            for (var e = 0; e < 8; e++)
            {
                var note = e % 4 == 2 ? c[2] - 24 : c[0] - 24;
                bass.Add(new N(t + e * eighth, eighth * 0.8, note, e % 2 == 0 ? 1.0 : 0.7));
            }

            // Chords land off the beat, so they kick against the bass rather than doubling it.
            foreach (var note in c)
            {
                stabs.Add(new N(t + eighth * 1, eighth * 0.7, note + 12, 0.5));
                stabs.Add(new N(t + eighth * 3, eighth * 0.7, note + 12, 0.45));
                stabs.Add(new N(t + eighth * 6, eighth * 0.7, note + 12, 0.5));
            }
        }

        // A running lead in mostly eighths, with a held note at the end of each four-bar phrase to breathe.
        var lead = new List<N>();
        int[][] phrases =
        [
            [74, 77, 79, 77, 74, 72, 74, 77],
            [79, 81, 79, 77, 74, 77, 74, 72],
            [74, 77, 81, 79, 77, 74, 72, 70],
            [72, 74, 77, 79, 81, 79, 77, 74],
            [77, 79, 81, 84, 81, 79, 77, 76],
        ];
        for (var p = 0; p < phrases.Length; p++)
        {
            var start = p * bar * 4;
            var step = bar / 4;                       // four notes a bar
            for (var i = 0; i < phrases[p].Length; i++)
                lead.Add(new N(start + i * step, step * 0.85, phrases[p][i]));

            // The back half of each phrase: a held note over the remaining two bars.
            lead.Add(new N(start + bar * 2, bar * 1.8, phrases[p][^1] + (p % 2 == 0 ? 5 : 3), 0.9));
        }

        return Render(30.0, (Bass, bass), (EPiano, stabs), (Horn, lead));
    }

    /// <summary>
    /// Registration: the one piece in the game that is purely looking forward. Bright major, a bouncing bass,
    /// and a melody that keeps stepping upward and landing higher than it started — the sound of signing your
    /// name to something before you know how it turns out.
    /// </summary>
    private static short[] Registration()
    {
        const double bar = 1.5;

        // C  G  Am F  C  G  F  G  C  Em F  G  Am F  G  C  F  G  C  C
        int[][] chords =
        [
            [60, 64, 67], [55, 59, 62], [57, 60, 64], [53, 57, 60],
            [60, 64, 67], [55, 59, 62], [53, 57, 60], [55, 59, 62],
            [60, 64, 67], [52, 55, 59], [53, 57, 60], [55, 59, 62],
            [57, 60, 64], [53, 57, 60], [55, 59, 62], [60, 64, 67],
            [53, 57, 60], [55, 59, 62], [60, 64, 67], [60, 64, 67],
        ];

        var bass = new List<N>();
        var comp = new List<N>();
        for (var barIndex = 0; barIndex < chords.Length; barIndex++)
        {
            var t = barIndex * bar;
            var c = chords[barIndex];
            var quarter = bar / 4;

            // A bouncing root-fifth-root-fifth, lighter than the battle theme's relentless eighths.
            bass.Add(new N(t, quarter * 0.7, c[0] - 24));
            bass.Add(new N(t + quarter, quarter * 0.7, c[2] - 24, 0.7));
            bass.Add(new N(t + quarter * 2, quarter * 0.7, c[0] - 24, 0.9));
            bass.Add(new N(t + quarter * 3, quarter * 0.7, c[2] - 24, 0.7));

            foreach (var note in c)
            {
                comp.Add(new N(t + quarter * 0.5, quarter * 0.45, note + 12, 0.45));
                comp.Add(new N(t + quarter * 2.5, quarter * 0.45, note + 12, 0.42));
            }
        }

        // Each phrase opens a step higher than the last, so the tune keeps climbing across the loop.
        var lead = new List<N>();
        int[][] phrases =
        [
            [72, 74, 76, 79, 76, 74],
            [74, 76, 79, 81, 79, 76],
            [76, 79, 81, 84, 81, 79],
            [79, 76, 74, 72, 74, 76],
            [72, 76, 79, 84, 79, 76],
        ];
        for (var p = 0; p < phrases.Length; p++)
        {
            var start = p * bar * 4;
            var step = bar / 3;
            for (var i = 0; i < phrases[p].Length; i++)
                lead.Add(new N(start + i * step, step * 0.8, phrases[p][i]));
            lead.Add(new N(start + bar * 3, bar * 0.9, phrases[p][0] + 12, 0.85));
        }

        // A bell on the downbeat of each phrase, like a little fanfare punctuating the climb.
        List<N> bells =
        [
            new(0.0, 0.8, 84, 0.4), new(6.0, 0.8, 88, 0.4),
            new(12.0, 0.8, 91, 0.38), new(18.0, 0.8, 88, 0.4), new(24.0, 0.8, 84, 0.4),
        ];

        return Render(30.0, (Bass, bass), (EPiano, comp), (Horn, lead), (Bell, bells));
    }

    // ---- Synthesis ---------------------------------------------------------------------------

    private static double Midi(int note) => 440.0 * Math.Pow(2, (note - 69) / 12.0);

    private static short[] Render(double seconds, params (Voice Voice, List<N> Notes)[] parts)
    {
        var buf = new float[(int)(seconds * SampleRate)];

        foreach (var (voice, notes) in parts)
            foreach (var note in notes)
                AddNote(buf, voice, note);

        // A short taper at each end. The loop point is a hard splice between the last sample and the first,
        // and without this the discontinuity is audible as a click every thirty seconds.
        const int fade = (int)(0.015 * SampleRate);
        for (var i = 0; i < fade && i < buf.Length; i++)
        {
            var g = i / (float)fade;
            buf[i] *= g;
            buf[buf.Length - 1 - i] *= g;
        }

        return Normalise(buf);
    }

    /// <summary>
    /// One FM note: a carrier whose phase is modulated by a second oscillator at a fixed ratio, with the
    /// depth of that modulation falling off over the note. That falling index is what makes an FM tone start
    /// bright and settle — the single most recognisable thing about the sound.
    /// </summary>
    private static void AddNote(float[] buf, Voice voice, N note)
    {
        var from = (int)(note.Start * SampleRate);
        var count = (int)(note.Duration * SampleRate);
        var freq = Midi(note.Midi);
        var amp = voice.Amp * note.Amp;

        for (var i = 0; i < count; i++)
        {
            var idx = from + i;
            if (idx < 0 || idx >= buf.Length)
                continue;

            var t = i / (double)SampleRate;
            var env = Envelope(t, note.Duration, voice.Attack, voice.Release);
            if (env <= 0)
                continue;

            var index = voice.Index * Math.Exp(-t * voice.IndexDecay);
            var modulator = Math.Sin(2 * Math.PI * freq * voice.Ratio * t);
            var sample = Math.Sin(2 * Math.PI * freq * t + index * modulator);

            // A second carrier a hair off pitch, for the voices that want to waver.
            if (voice.Detune > 0)
            {
                var detuned = freq * (1 + voice.Detune / 100.0);
                sample = 0.5 * sample + 0.5 * Math.Sin(2 * Math.PI * detuned * t + index * modulator);
            }

            buf[idx] += (float)(sample * env * amp);
        }
    }

    private static double Envelope(double t, double duration, double attack, double release)
    {
        if (t < 0 || t > duration)
            return 0;
        if (t < attack)
            return t / attack;

        var untilEnd = duration - t;
        if (untilEnd < release)
            return Math.Max(0, untilEnd / release);

        // Instruments do not hold dead flat; a slight sag keeps sustained chords from sounding synthetic.
        return 1.0 - 0.18 * ((t - attack) / Math.Max(1e-6, duration - attack));
    }

    /// <summary>Roughly how loud each track should feel, in RMS. Matched across the set so switching rooms
    /// never also changes the volume.</summary>
    private const double TargetRms = 0.19;

    /// <summary>The most any single sample may reach, so a track with hard attacks still cannot clip.</summary>
    private const double PeakCeiling = 0.85;

    /// <summary>
    /// Levelled by loudness rather than by peak. Normalising to a fixed peak makes a piece with sharp attacks
    /// — the battle theme, all stabs and short bass notes — measurably and audibly quieter than a piece of
    /// sustained chords, which is exactly backwards for the one that is supposed to raise the pulse.
    /// </summary>
    private static short[] Normalise(float[] buf)
    {
        double sumSquares = 0;
        var max = 0.0;
        foreach (var v in buf)
        {
            sumSquares += (double)v * v;
            max = Math.Max(max, Math.Abs(v));
        }

        var rms = buf.Length > 0 ? Math.Sqrt(sumSquares / buf.Length) : 0;
        if (rms <= 1e-6 || max <= 1e-6)
            return new short[buf.Length];

        var gain = Math.Min(TargetRms / rms, PeakCeiling / max);

        var output = new short[buf.Length];
        for (var i = 0; i < buf.Length; i++)
            output[i] = (short)Math.Clamp(buf[i] * gain * short.MaxValue, short.MinValue, short.MaxValue);
        return output;
    }
}
