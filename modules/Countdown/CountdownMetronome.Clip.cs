using System;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.Countdown;
internal sealed partial class CountdownMetronome {
    private const float FallbackAccentGain = 1.35f;
    private static AudioClip CreateLoopClip(
        AudioClip hatClip,
        AudioClip kickClip,
        MetronomeSettings settings,
        out int loopFrames
    ) {
        double interval = 60.0 / settings.ClickBpm;
        loopFrames = Math.Max(1, (int)Math.Round(hatClip.frequency * interval));
        if(!TryCreateClickSamples(hatClip, hatClip.frequency, hatClip.channels, loopFrames, out float[] weakSamples))
            throw new InvalidOperationException("sndHat sample data is unavailable.");
        if(kickClip == null
            || !TryCreateClickSamples(kickClip, hatClip.frequency, hatClip.channels, loopFrames, out float[] accentSamples))
            accentSamples = CreateAmplifiedCopy(weakSamples, FallbackAccentGain);
        int samplesPerBeat = loopFrames * hatClip.channels;
        float[] loopSamples = new float[samplesPerBeat * settings.Numerator];
        Array.Copy(accentSamples, 0, loopSamples, 0, samplesPerBeat);
        for(int beatIndex = 1; beatIndex < settings.Numerator; beatIndex++)
            Array.Copy(weakSamples, 0, loopSamples, beatIndex * samplesPerBeat, samplesPerBeat);
        AudioClip loopClip = AudioClip.Create(
            "Quartz Countdown Metronome",
            loopFrames * settings.Numerator,
            hatClip.channels,
            hatClip.frequency,
            stream: false
        );
        if(loopClip == null || !loopClip.SetData(loopSamples, 0)) {
            DestroyObject(loopClip);
            throw new InvalidOperationException("The metronome loop sample data could not be assigned.");
        }
        return loopClip;
    }
    private static bool TryCreateClickSamples(
        AudioClip sourceClip,
        int targetFrequency,
        int targetChannels,
        int targetFrames,
        out float[] targetSamples
    ) {
        targetSamples = null;
        if(sourceClip == null
            || sourceClip.samples <= 0
            || sourceClip.channels <= 0
            || sourceClip.frequency <= 0
            || targetFrequency <= 0
            || targetChannels <= 0
            || targetFrames <= 0)
            return false;
        try {
            int sourceChannels = sourceClip.channels;
            int sourceFrames = sourceClip.samples;
            float[] sourceSamples = new float[sourceFrames * sourceChannels];
            if(!sourceClip.GetData(sourceSamples, 0)) return false;
            targetSamples = ConvertSamples(
                sourceSamples,
                sourceFrames,
                sourceChannels,
                sourceClip.frequency,
                targetFrames,
                targetChannels,
                targetFrequency
            );
            return true;
        } catch(Exception e) {
            Diag.Ignore(e);
            targetSamples = null;
            return false;
        }
    }
    private static float[] ConvertSamples(
        float[] sourceSamples,
        int sourceFrames,
        int sourceChannels,
        int sourceFrequency,
        int targetFrames,
        int targetChannels,
        int targetFrequency
    ) {
        float[] targetSamples = new float[targetFrames * targetChannels];
        double sourceFramesPerTargetFrame = (double)sourceFrequency / targetFrequency;
        for(int targetFrame = 0; targetFrame < targetFrames; targetFrame++) {
            double sourcePosition = targetFrame * sourceFramesPerTargetFrame;
            int lowerSourceFrame = (int)sourcePosition;
            if(lowerSourceFrame >= sourceFrames) break;
            int upperSourceFrame = Math.Min(lowerSourceFrame + 1, sourceFrames - 1);
            float interpolation = (float)(sourcePosition - lowerSourceFrame);
            for(int targetChannel = 0; targetChannel < targetChannels; targetChannel++) {
                float lowerSample =
                    ReadChannelSample(sourceSamples, lowerSourceFrame, sourceChannels, targetChannel, targetChannels);
                float upperSample =
                    ReadChannelSample(sourceSamples, upperSourceFrame, sourceChannels, targetChannel, targetChannels);
                targetSamples[targetFrame * targetChannels + targetChannel] =
                    lowerSample + (upperSample - lowerSample) * interpolation;
            }
        }
        return targetSamples;
    }
    private static float ReadChannelSample(
        float[] samples,
        int frame,
        int sourceChannels,
        int targetChannel,
        int targetChannels
    ) {
        int frameOffset = frame * sourceChannels;
        if(targetChannels == 1 && sourceChannels > 1) {
            float sum = 0f;
            for(int sourceChannel = 0; sourceChannel < sourceChannels; sourceChannel++)
                sum += samples[frameOffset + sourceChannel];
            return sum / sourceChannels;
        }
        int sourceChannelIndex = sourceChannels == 1 ? 0 : Math.Min(targetChannel, sourceChannels - 1);
        return samples[frameOffset + sourceChannelIndex];
    }
    private static float[] CreateAmplifiedCopy(float[] samples, float gain) {
        float[] amplified = new float[samples.Length];
        for(int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            amplified[sampleIndex] = Mathf.Clamp(samples[sampleIndex] * gain, -1f, 1f);
        return amplified;
    }
}
