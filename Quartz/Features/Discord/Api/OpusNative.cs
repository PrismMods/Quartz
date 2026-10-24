using System.Runtime.InteropServices;
using Quartz.Core;
namespace Quartz.Features.Discord;
public static class OpusNative {
    public const int SampleRate = 48000;
    public const int Channels = 1;
    public const int FrameSamples = 960;
    private const int ApplicationVoip = 2048;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr EncoderCreateFn(int sampleRate, int channels, int application, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EncoderDestroyFn(IntPtr encoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EncodeFn(IntPtr encoder, IntPtr pcm, int frameSize, IntPtr data, int maxBytes);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr DecoderCreateFn(int sampleRate, int channels, out int error);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DecoderDestroyFn(IntPtr decoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int DecodeFn(IntPtr decoder, IntPtr data, int length, IntPtr pcm, int frameSize, int useFec);
    private static EncoderCreateFn encoderCreate;
    private static EncoderDestroyFn encoderDestroy;
    private static EncodeFn encode;
    private static DecoderCreateFn decoderCreate;
    private static DecoderDestroyFn decoderDestroy;
    private static DecodeFn decode;
    public static bool Available { get; private set; }
    public static string LoadError { get; private set; } = "not loaded";
    public static bool Load() {
        if(Available) return true;
        string path = VoiceNatives.Locate("opus");
        if(path == null) {
            LoadError = "libopus is not installed";
            return false;
        }
        IntPtr library = NativeLib.Load(path);
        if(library == IntPtr.Zero) {
            LoadError = "the operating system refused to load " + Path.GetFileName(path);
            return false;
        }
        encoderCreate = NativeLib.Bind<EncoderCreateFn>(library, "opus_encoder_create");
        encoderDestroy = NativeLib.Bind<EncoderDestroyFn>(library, "opus_encoder_destroy");
        encode = NativeLib.Bind<EncodeFn>(library, "opus_encode");
        decoderCreate = NativeLib.Bind<DecoderCreateFn>(library, "opus_decoder_create");
        decoderDestroy = NativeLib.Bind<DecoderDestroyFn>(library, "opus_decoder_destroy");
        decode = NativeLib.Bind<DecodeFn>(library, "opus_decode");
        if(encoderCreate == null || encode == null || decoderCreate == null || decode == null) {
            LoadError = "libopus is missing one of the encode/decode entry points";
            return false;
        }
        Available = true;
        LoadError = "";
        return true;
    }
    public static IntPtr CreateEncoder(out string error) {
        error = null;
        IntPtr encoder = encoderCreate(SampleRate, Channels, ApplicationVoip, out int code);
        if(encoder == IntPtr.Zero || code != 0) {
            error = "opus_encoder_create returned " + code;
            return IntPtr.Zero;
        }
        return encoder;
    }
    public static IntPtr CreateDecoder(out string error) {
        error = null;
        IntPtr decoder = decoderCreate(SampleRate, Channels, out int code);
        if(decoder == IntPtr.Zero || code != 0) {
            error = "opus_decoder_create returned " + code;
            return IntPtr.Zero;
        }
        return decoder;
    }
    public static void DestroyEncoder(IntPtr encoder) {
        if(encoder != IntPtr.Zero) encoderDestroy?.Invoke(encoder);
    }
    public static void DestroyDecoder(IntPtr decoder) {
        if(decoder != IntPtr.Zero) decoderDestroy?.Invoke(decoder);
    }
    public static unsafe int Encode(IntPtr encoder, short[] pcm, int frameSamples, byte[] output) {
        if(encoder == IntPtr.Zero || pcm == null || output == null
            || frameSamples <= 0 || frameSamples > pcm.Length || output.Length == 0) return -1;
        fixed(short* pcmBuffer = pcm)
        fixed(byte* outBuffer = output) {
            return encode(
                encoder,
                (IntPtr)pcmBuffer,
                frameSamples,
                (IntPtr)outBuffer,
                output.Length
            );
        }
    }
    public static unsafe int Decode(IntPtr decoder, byte[] payload, int length, short[] pcm, int frameSamples) {
        if(decoder == IntPtr.Zero || pcm == null || frameSamples <= 0 || frameSamples > pcm.Length
            || length < 0 || (length > 0 && (payload == null || length > payload.Length))) return -1;
        fixed(byte* input = payload)
        fixed(short* output = pcm) {
            return decode(
                decoder,
                length == 0 ? IntPtr.Zero : (IntPtr)input,
                length,
                (IntPtr)output,
                frameSamples,
                0
            );
        }
    }
}
