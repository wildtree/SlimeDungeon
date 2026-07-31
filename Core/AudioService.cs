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

        Available = true;
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
        foreach (var stream in _streams)
            SDL.DestroyAudioStream(stream);
        _streams.Clear();

        if (Available)
            SDL.QuitSubSystem(SDL.InitFlags.Audio);
    }
}
