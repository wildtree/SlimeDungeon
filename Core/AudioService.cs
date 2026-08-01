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

    private readonly Dictionary<MusicId, byte[]> _musicBytes = new();
    private MusicId? _nowPlaying;

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
            foreach (var (id, samples) in MusicBank.BuildAll())
                _musicBytes[id] = ToBytes(samples);
        }

        Available = true;
    }

    /// <summary>
    /// Switches the background track. Asking for the one already playing does nothing, so screens are free to
    /// call this every frame — which is how moving between the guild and its various counters stays seamless.
    /// Passing null fades the music out by simply letting the queue drain.
    /// </summary>
    public void PlayMusic(MusicId? id)
    {
        if (!Available || _musicStream == IntPtr.Zero || _nowPlaying == id)
            return;

        _nowPlaying = id;
        SDL.ClearAudioStream(_musicStream);
        if (id is { } wanted && _musicBytes.TryGetValue(wanted, out var data))
            SDL.PutAudioStreamData(_musicStream, data, data.Length);
    }

    /// <summary>
    /// Keeps the current track looping. Call once a frame: when the queued audio runs low another copy of the
    /// track is appended, which joins onto the end of what is already queued without a gap.
    /// </summary>
    public void UpdateMusic()
    {
        if (!Available || _musicStream == IntPtr.Zero || _nowPlaying is not { } id)
            return;
        if (!_musicBytes.TryGetValue(id, out var data))
            return;

        var queuedBytes = SDL.GetAudioStreamQueued(_musicStream);
        var queuedSeconds = queuedBytes / (float)(MusicBank.SampleRate * sizeof(short));
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
