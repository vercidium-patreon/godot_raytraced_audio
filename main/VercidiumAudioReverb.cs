namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public ALReverbEffect GetReverbEffect(vaudio.Voice voice)
    {
        if (voice.groupedEAXIndex >= 0)
            return groupedReverbEffects[voice.groupedEAXIndex];
        else
            return listenerReverbEffect;
    }

    void UpdateGodotReverb()
    {
        // Update ambient gain
        {
            var ambientClarity = Lerp(0.0f, 1.0f, MathF.Min(1, context.ProcessedReverb.OutsidePercent / 0.4f));
            float gain = 0.3f + ambientClarity * 0.7f;
            float gainHF = MathF.Pow(gain, 1.5f);

            ambientFilter ??= new(gain, gainHF);
            ambientFilter.SetGain(gain, gainHF);
        }

        // Apply raytraced EAX results to ALReverbEffects
        CopyReverb(context.ListenerEAX, listenerReverbEffect, false);

        for (int i = 0; i < context.GroupedEAX.Count; i++)
        {
            if (groupedReverbEffects.Count <= i)
                groupedReverbEffects.Add(new());

            CopyReverb(context.GroupedEAX[i], groupedReverbEffects[i], true);

            groupedReverbEffects[i].Update();
        }
    }

    void CopyReverb(vaudio.EAXReverbResults eax, ALReverbEffect effect, bool isGroupedEAX)
    {
        effect.gain = 1;

        // Density causes static when updating in real time
        //  See OpenAL Soft GitHub issue: https://github.com/kcat/openal-soft/issues/1229
        effect.density = 0.5f;//eax.Density;

        effect.diffusion = eax.Diffusion;
        effect.gainLF = eax.GainLF;
        effect.gainHF = eax.GainHF;
        effect.decayTime = eax.DecayTime;
        effect.decayLFRatio = eax.DecayLFRatio;
        effect.decayHFRatio = eax.DecayHFRatio;
        effect.reflectionsDelay = eax.ReflectionsDelay;
        effect.reflectionsGain = eax.ReflectionsGain;
        effect.lateReverbGain = eax.LateReverbGain;
        effect.lateReverbDelay = eax.LateReverbDelay;
        effect.echoTime = eax.EchoTime;
        effect.echoDepth = eax.EchoDepth;
        effect.modulationTime = eax.ModulationTime;
        effect.modulationDepth = eax.ModulationDepth;
        effect.airAbsorptionGainHF = eax.AirAbsorptionGainHF;
        effect.hfReference = eax.HFReference;
        effect.lfReference = eax.LFReference;
        effect.roomRolloffFactor = eax.RoomRolloffFactor;
        effect.decayHFLimit = eax.DecayHFLimit;

        // TODO - less hardcoded reverb panning logic
        if (isGroupedEAX)
        {
            // The gain is the average of all grouped voice's gainLF and gainHF
            effect.effectSlotGain = eax.ALEffectSlotGain;


            // Get the difference from the camera to the reverb center
            var camera = GetViewport().GetCamera3D();
            var pos = camera.GlobalPosition;

            var cameraPosition = new vaudio.Vector3F(pos.X, pos.Y, pos.Z);

            var diff = cameraPosition - eax.Center;
            var mag = diff.Magnitude;


            // Interpolate from PanAL to (0, 0, 0) when we're inside the same room
            eax.PanAL = eax.PanAL.Normalized;

            var roomRadius = (eax.BoundsMax - eax.BoundsMin).Magnitude;
            roomRadius = MathF.Pow(roomRadius, 0.77f);

            var smoothDistance = 2.5f;

            if (mag < roomRadius)
            {
                var threshold = roomRadius - smoothDistance;
                var strength = Math.Max(0, mag - threshold) / smoothDistance;

                eax.PanAL *= strength;
            }


            // Handle normalisation failures
            if (IsNaNorInfinity(eax.PanAL))
                eax.PanAL = vaudio.Vector3F.Zero;
        }

        // TODO - separate pan for late reverb and reflections
        effect.lateReverbPan[0] = eax.PanAL.X;
        effect.lateReverbPan[1] = eax.PanAL.Y;
        effect.lateReverbPan[2] = eax.PanAL.Z;

        effect.reflectionsPan[0] = eax.PanAL.X;
        effect.reflectionsPan[1] = eax.PanAL.Y;
        effect.reflectionsPan[2] = eax.PanAL.Z;

        effect.dirty = true;
        effect.Update();
    }
}
