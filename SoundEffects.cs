using System.Runtime.InteropServices;
using System.Text;

namespace MinesweeperOnSteroids;

internal enum SoundCue { Deploy, Reveal, Flag, Unflag, Radar, Combo, Shield, Explosion, Victory }

internal sealed class SoundEffects : IDisposable
{
    private readonly Dictionary<SoundCue, GCHandle> sounds = [];
    private bool enabled = true;
    private bool playing;
    public string? Warning { get; private set; }

    public bool Enabled
    {
        get => enabled;
        set
        {
            enabled = value;
            if (!value)
                Stop();
        }
    }

    public void Play(SoundCue cue)
    {
        if (!enabled)
            return;
        Stop();
        if (!sounds.TryGetValue(cue, out GCHandle sound))
        {
            sound = GCHandle.Alloc(CreateWave(cue), GCHandleType.Pinned);
            sounds.Add(cue, sound);
        }
        // WinMM borrows this buffer for asynchronous playback; keep it pinned until Stop.
        playing = PlaySound(sound.AddrOfPinnedObject(), IntPtr.Zero, 0x0001 | 0x0004 | 0x0002);
        Warning = playing ? null : "Sound playback unavailable. Check your Windows output device, or mute sound.";
    }

    public void Stop()
    {
        if (playing)
        {
            PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
            playing = false;
        }
    }

    internal static byte[] CreateWave(SoundCue cue)
    {
        const int sampleRate = 22050;
        double duration = cue switch
        {
            SoundCue.Victory => .95,
            SoundCue.Explosion => .65,
            SoundCue.Shield => .45,
            SoundCue.Radar => .42,
            SoundCue.Deploy => .32,
            SoundCue.Combo => .22,
            _ => .09
        };
        int samples = (int)(duration * sampleRate);
        using var buffer = new MemoryStream(44 + samples * 2);
        using var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        var noise = new Random(1984 + (int)cue);
        double phase = 0;
        double[] melody = [523.25, 659.25, 783.99, 1046.5];
        for (int i = 0; i < samples; i++)
        {
            double time = (double)i / sampleRate;
            double progress = time / duration;
            double frequency = cue switch
            {
                SoundCue.Deploy => 240 + 900 * progress,
                SoundCue.Reveal => 760 - 260 * progress,
                SoundCue.Flag => 440 + 440 * progress,
                SoundCue.Unflag => 600 - 300 * progress,
                SoundCue.Radar => 900 + 400 * Math.Sin(progress * Math.PI * 3),
                SoundCue.Combo => 880 + 880 * progress,
                SoundCue.Shield => 160 + 1600 * (1 - progress) * (1 - progress),
                SoundCue.Explosion => 90 - 65 * progress,
                SoundCue.Victory => melody[Math.Min(3, (int)(progress * 4))],
                _ => throw new ArgumentOutOfRangeException(nameof(cue))
            };
            phase += 2 * Math.PI * frequency / sampleRate;
            double tone = Math.Sin(phase) * .75 + Math.Sin(phase * 2) * .15;
            if (cue is SoundCue.Explosion or SoundCue.Shield)
                tone = tone * .4 + (noise.NextDouble() * 2 - 1) * .6;
            double envelope = Math.Min(1, time / .008) * Math.Min(1, (duration - time) / .025) *
                              Math.Exp(-progress * (cue == SoundCue.Victory ? .6 : 2.5));
            writer.Write((short)(Math.Clamp(tone * envelope * .25, -1, 1) * short.MaxValue));
        }
        writer.Flush();
        return buffer.ToArray();
    }

    public void Dispose()
    {
        Stop();
        foreach (GCHandle sound in sounds.Values)
            sound.Free();
        sounds.Clear();
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
