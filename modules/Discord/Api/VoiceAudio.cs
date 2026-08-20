using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Discord;
public sealed class VoiceAudio : MonoBehaviour {
    private const int ClipSeconds = 1;
    private const int JitterCap = OpusNative.SampleRate * 2;
    private static VoiceAudio instance;
    private static readonly Queue<float> jitter = new();
    private static readonly object jitterLock = new();
    private readonly Dictionary<uint, IntPtr> decoders = [];
    private VoiceUdp transport;
    private VoiceGateway gateway;
    private AudioSource output;
    private AudioClip microphone;
    private string device;
    private IntPtr encoder;
    private int readPosition;
    private readonly List<short> pending = [];
    private float[] scratch;
    private short[] frame;
    private byte[] encoded;
    private bool speaking;
    public static bool Muted { get; set; }
    public static DaveSession Dave { get; set; }
    public static int FramesSent { get; private set; }
    public static int FramesReceived { get; private set; }
    public static int FramesDropped { get; private set; }
    public static float MicLevel { get; private set; }
    public static float MicPeak { get; private set; }
    public static bool Capturing { get; private set; }
    public static string Status { get; private set; } = "idle";
    public static void Begin(VoiceUdp udp, VoiceGateway voiceGateway) {
        if(!OpusNative.Load()) {
            Status = "opus unavailable — " + OpusNative.LoadError;
            return;
        }
        if(!SodiumNative.Load()) {
            Status = "sodium unavailable — " + SodiumNative.LoadError;
            return;
        }
        if(instance == null) {
            GameObject host = new("QuartzDiscordVoice");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            instance = host.AddComponent<VoiceAudio>();
        }
        instance.Attach(udp, voiceGateway);
    }
    public static void End() {
        if(instance != null) instance.Detach();
        Status = "idle";
    }
    private void Attach(VoiceUdp udp, VoiceGateway voiceGateway) {
        Detach();
        transport = udp;
        gateway = voiceGateway;
        udp.AudioReceived += OnAudioReceived;
        StartPlayback();
        StartCapture();
    }
    private void Detach() {
        if(transport != null) transport.AudioReceived -= OnAudioReceived;
        transport = null;
        gateway = null;
        StopCapture();
        lock(jitterLock) jitter.Clear();
        foreach(KeyValuePair<uint, IntPtr> pair in decoders) OpusNative.DestroyDecoder(pair.Value);
        decoders.Clear();
        if(output != null) output.Stop();
    }
    private void StartPlayback() {
        if(output == null) {
            output = gameObject.AddComponent<AudioSource>();
            output.spatialBlend = 0f;
            output.loop = true;
            output.bypassEffects = true;
            output.bypassListenerEffects = true;
        }
        output.clip = AudioClip.Create(
            "QuartzDiscordVoice", OpusNative.SampleRate, OpusNative.Channels,
            OpusNative.SampleRate, true, OnPcmRead);
        output.Play();
    }
    private void StartCapture() {
        try {
            string[] devices = Microphone.devices;
            if(devices == null || devices.Length == 0) {
                Status = "no microphone was found";
                return;
            }
            device = devices[0];
            Microphone.GetDeviceCaps(device, out int minFrequency, out int maxFrequency);
            microphone = Microphone.Start(device, true, ClipSeconds, OpusNative.SampleRate);
            if(microphone == null) {
                Status = "the microphone refused to start";
                return;
            }
            encoder = OpusNative.CreateEncoder(out string error);
            if(encoder == IntPtr.Zero) {
                Status = error;
                return;
            }
            scratch = new float[OpusNative.SampleRate];
            frame = new short[OpusNative.FrameSamples];
            encoded = new byte[4000];
            readPosition = 0;
            pending.Clear();
            FramesSent = 0;
        FramesReceived = 0;
        FramesDropped = 0;
        MicPeak = 0f;
        MicLevel = 0f;
        Capturing = true;
            Status = $"capturing from {device} ({minFrequency}-{maxFrequency}Hz)";
            MainCore.Log.Msg($"[Discord] voice capture started on '{device}'");
        } catch(Exception e) {
            Status = "microphone failed: " + e.Message;
            MainCore.Log.Wrn("[Discord] voice capture failed: " + e);
        }
    }
    private void StopCapture() {
        Capturing = false;
        if(microphone != null && device != null) {
            try {
                Microphone.End(device);
            } catch(Exception e) {
                Diag.Ignore(e);
            }
        }
        microphone = null;
        device = null;
        if(encoder != IntPtr.Zero) {
            OpusNative.DestroyEncoder(encoder);
            encoder = IntPtr.Zero;
        }
        speaking = false;
    }
    private void Update() {
        if(!Capturing || microphone == null || transport == null || !transport.Ready) return;
        int position;
        try {
            position = Microphone.GetPosition(device);
        } catch(Exception e) {
            Diag.Ignore(e);
            return;
        }
        int available = position - readPosition;
        if(available < 0) available += microphone.samples;
        if(available <= 0) return;
        if(available > scratch.Length) available = scratch.Length;
        if(!microphone.GetData(scratch, readPosition)) return;
        readPosition = (readPosition + available) % microphone.samples;
        if(Muted) {
            if(speaking) SetSpeaking(false);
            pending.Clear();
            return;
        }
        float peak = 0f;
        for(int i = 0; i < available; i++) {
            float sample = Mathf.Clamp(scratch[i], -1f, 1f);
            float magnitude = sample < 0f ? -sample : sample;
            if(magnitude > peak) peak = magnitude;
            pending.Add((short)(sample * short.MaxValue));
        }
        MicLevel = peak;
        if(peak > MicPeak) MicPeak = peak;
        while(pending.Count >= OpusNative.FrameSamples) {
            pending.CopyTo(0, frame, 0, OpusNative.FrameSamples);
            pending.RemoveRange(0, OpusNative.FrameSamples);
            int written = OpusNative.Encode(encoder, frame, OpusNative.FrameSamples, encoded);
            if(written <= 0) continue;
            if(!speaking) SetSpeaking(true);
            DaveSession dave = Dave;
            if(dave != null) {
                byte[] wrapped = dave.EncryptOpus(encoded, written);
                transport.SendAudio(wrapped, wrapped.Length);
            } else {
                transport.SendAudio(encoded, written);
            }
            FramesSent++;
        }
    }
    private void SetSpeaking(bool value) {
        speaking = value;
        if(gateway == null) return;
        try {
            _ = gateway.SendSpeakingAsync(value, gateway.Ssrc);
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    private void OnAudioReceived(uint ssrc, byte[] payload) {
        try {
            byte[] opus = payload;
            DaveSession dave = Dave;
            if(dave != null && dave.KeysReady) {
                opus = dave.DecryptOpus(ssrc, payload);
                if(opus == null) {
                    FramesDropped++;
                    return;
                }
            }
            FramesReceived++;
            if(!decoders.TryGetValue(ssrc, out IntPtr decoder)) {
                decoder = OpusNative.CreateDecoder(out string error);
                if(decoder == IntPtr.Zero) {
                    MainCore.Log.Wrn("[Discord] " + error);
                    return;
                }
                decoders[ssrc] = decoder;
            }
            short[] pcm = new short[OpusNative.FrameSamples];
            int samples = OpusNative.Decode(decoder, opus, opus.Length, pcm, OpusNative.FrameSamples);
            if(samples <= 0) return;
            lock(jitterLock) {
                if(jitter.Count > JitterCap) jitter.Clear();
                for(int i = 0; i < samples; i++) jitter.Enqueue(pcm[i] / (float)short.MaxValue);
            }
        } catch(Exception e) {
            Diag.Ignore(e);
        }
    }
    private static void OnPcmRead(float[] data) {
        lock(jitterLock) {
            for(int i = 0; i < data.Length; i++) data[i] = jitter.Count > 0 ? jitter.Dequeue() : 0f;
        }
    }
    private void OnDestroy() => Detach();
}
