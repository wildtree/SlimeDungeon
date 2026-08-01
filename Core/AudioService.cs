using SDL3;

namespace SlimeDungeon.Core;

/// <summary>
/// Plays the synthesised sound bank. A small pool of audio streams is bound to the default device, because
/// data pushed into one stream queues up behind whatever is already in it — a fanfare would hold up every hit
/// that landed under it. Handing each sound its own free stream lets SDL mix them.
///
/// Every failure path here is silent by design: a machine with no working audio device should still play the
/// game, so a device that will not open leaves <see cref="Available"/> false and every Play call a no-op.
/// </summary>
public sealed class AudioService : IDisposable
{
    /// <summary>Enough to overlap a running fanfare with several combat hits without ever stealing a voice.</summary>
    private const int StreamCount = 8;

    private readonly List<IntPtr> _streams = new();
    private readonly IReadOnlyDictionary<SoundId, short[]> _clips;
    private readonly Dictionary<SoundId, byte[]> _bytes = new();
    private int _next;

    /// <summary>
    /// Music gets a stream of its own rather than sharing the effects pool: it needs its own volume, and it
    /// has to be topped up continuously to loop, which would fight with effects for a shared voice.
    /// </summary>
    private IntPtr _musicStream;

    /// <summary>
    /// Built on a worker thread and published here in one go when finished. Five thirty-second tracks take
    /// most of a second to synthesise, which is a second of the window not appearing if it is done inline.
    /// Read through <see cref="Volatile"/> because the thread that writes it is not the one that reads it.
    /// </summary>
    private Dictionary<MusicId, byte[]>? _musicBytes;

    /// <summary>What the game has asked for, and what is actually queued on the device — they differ while a
    /// track is waiting out a delay, or while the music is still being generated.</summary>
    private MusicId? _requested;
    private MusicId? _started;
    private float _startDelay;

    /// <summary>
    /// Background music sits well under the effects so it never competes with a hit or a fanfare. At 0.32 the
    /// measured loudness of a track was within a few percent of a weapon hit's, which is not background music
    /// — this puts it roughly 10dB below the effects, quiet enough to notice only when listening for it.
    /// </summary>
    private const float MusicGain = 0.2f;

    /// <summary>
    /// Refill once the queue drops below this many seconds. Comfortably longer than a frame's worth of jitter
    /// and shorter than the track, so the next copy is always queued before the current one runs dry.
    /// </summary>
    private const float RefillBelowSeconds = 3f;

    public bool Available { get; }

    public AudioService()
    {
        _clips = SoundBank.BuildAll();
        foreach (var (id, samples) in _clips)
            _bytes[id] = ToBytes(samples);

        if (!SDL.InitSubSystem(SDL.InitFlags.Audio))
        {
            Console.Error.WriteLine($"audio unavailable, continuing without sound: {SDL.GetError()}");
            return;
        }

        var spec = new SDL.AudioSpec
        {
            Format = SDL.AudioFormat.AudioS16LE,
            Channels = 1,
            Freq = SoundBank.SampleRate,
        };

        for (var i = 0; i < StreamCount; i++)
        {
            var stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in spec, null, IntPtr.Zero);
            if (stream == IntPtr.Zero)
                break;
            SDL.ResumeAudioStreamDevice(stream);
            _streams.Add(stream);
        }

        if (_streams.Count == 0)
        {
            Console.Error.WriteLine($"no audio stream could be opened, continuing without sound: {SDL.GetError()}");
            return;
        }

        // Music is generated at half the effects' sample rate; SDL converts it up to whatever the device runs at.
        var musicSpec = new SDL.AudioSpec
        {
            Format = SDL.AudioFormat.AudioS16LE,
            Channels = 1,
            Freq = MusicBank.SampleRate,
        };
        _musicStream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in musicSpec, null, IntPtr.Zero);
        if (_musicStream != IntPtr.Zero)
        {
            SDL.SetAudioStreamGain(_musicStream, MusicGain);
            SDL.ResumeAudioStreamDevice(_musicStream);

            // Generated off the startup path; whatever was requested in the meantime starts as soon as this
            // lands. A second or so of silence at the title screen is a far better trade than a second of
            // black window before it appears.
            Task.Run(() =>
            {
                var built = new Dictionary<MusicId, byte[]>();
                foreach (var (id, samples) in MusicBank.BuildAll())
                    built[id] = ToBytes(samples);
                Volatile.Write(ref _musicBytes, built);
            });
        }

        Available = true;
    }

    /// <summary>
    /// Asks for a background track. Requesting the one already asked for does nothing, so screens are free to
    /// call this every frame — which is how moving between the guild and its various counters stays seamless.
    /// Null means silence.
    /// </summary>
    /// <param name="delaySeconds">
    /// Hold the change for this long before switching. Combat uses it so the encounter sting is heard on its
    /// own rather than under the first bars of the battle theme; the outgoing track keeps playing until then.
    /// </param>
    public void PlayMusic(MusicId? id, float delaySeconds = 0)
    {
        if (!Available || _musicStream == IntPtr.Zero || _requested == id)
            return;

        _requested = id;
        _startDelay = delaySeconds;
    }

    /// <summary>
    /// Starts whatever has been requested once it is due and available, and keeps the running track looping.
    /// Call once a frame: when the queued audio runs low another copy is appended, joining the end of what is
    /// already queued without a gap.
    /// </summary>
    public void UpdateMusic(float dt)
    {
        if (!Available || _musicStream == IntPtr.Zero)
            return;

        var bank = Volatile.Read(ref _musicBytes);
        if (bank is null)
            return;

        if (_startDelay > 0)
            _startDelay = Math.Max(0, _startDelay - dt);

        // Switch tracks once the requested one is due.
        if (_started != _requested && _startDelay <= 0)
        {
            SDL.ClearAudioStream(_musicStream);
            _started = _requested;
            if (_started is { } starting && bank.TryGetValue(starting, out var fresh))
                SDL.PutAudioStreamData(_musicStream, fresh, fresh.Length);
        }

        if (_started is not { } playing || !bank.TryGetValue(playing, out var data))
            return;

        var queuedSeconds = SDL.GetAudioStreamQueued(_musicStream) / (float)(MusicBank.SampleRate * sizeof(short));
        if (queuedSeconds < RefillBelowSeconds)
            SDL.PutAudioStreamData(_musicStream, data, data.Length);
    }

    /// <summary>
    /// Starts a sound. Prefers a stream that has finished whatever it was playing; if every one is busy the
    /// oldest is cleared and reused, so a burst of effects drops the stalest sound rather than queueing up a
    /// backlog that would play out of time with the battle.
    /// </summary>
    public void Play(SoundId id)
    {
        if (!Available || !_bytes.TryGetValue(id, out var data))
            return;

        var stream = FindFreeStream() ?? TakeOldestStream();
        SDL.PutAudioStreamData(stream, data, data.Length);
    }

    private IntPtr? FindFreeStream()
    {
        foreach (var stream in _streams)
        {
            if (SDL.GetAudioStreamQueued(stream) <= 0)
                return stream;
        }
        return null;
    }

    private IntPtr TakeOldestStream()
    {
        var stream = _streams[_next];
        _next = (_next + 1) % _streams.Count;
        SDL.ClearAudioStream(stream);
        return stream;
    }

    private static byte[] ToBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * sizeof(short)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public void Dispose()
    {
        if (_musicStream != IntPtr.Zero)
        {
            SDL.DestroyAudioStream(_musicStream);
            _musicStream = IntPtr.Zero;
        }

        foreach (var stream in _streams)
            SDL.DestroyAudioStream(stream);
        _streams.Clear();

        if (Available)
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
    }
}
