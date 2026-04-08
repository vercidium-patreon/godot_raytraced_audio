namespace godot_raytraced_audio;

public partial class VercidiumAudio : Node
{
    public ALReverbEffect GetReverbEffect(vaudio.Emitter emitter)
    {
        if (emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbEffects.Count} EAX presets are available.");
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        return listenerReverbEffect;
    }

    public ALReverbEffect GetReverbEffect(VercidiumAudioEmitter emitter)
    {
        if (emitter.GroupedEAXIndex >= 0)
        {
            if (emitter.GroupedEAXIndex >= groupedReverbEffects.Count)
            {
                LogWarning($"Emitter {emitter.Name} has a grouped EAX index of {emitter.GroupedEAXIndex} but only {groupedReverbEffects.Count} EAX presets are available.");
                return listenerReverbEffect;
            }

            return groupedReverbEffects[emitter.GroupedEAXIndex];
        }

        return listenerReverbEffect;
    }

    void OnReverbUpdated()
    {
        // Update ambient gain
        {
            var ambientClarityLF = listener.ProcessedReverb.OutsidePercent;
            var ambientClarityHF = listener.ProcessedReverb.OutsidePercent;
            
            // * 2 because half go into the terrain
            ambientClarityLF += listener.AmbientPermeationGainLF;
            ambientClarityHF += listener.AmbientPermeationGainHF;

            ambientClarityLF = MathF.Min(1, ambientClarityLF);
            ambientClarityHF = MathF.Min(1, ambientClarityHF);

            ambientFilter ??= new(ambientClarityLF, ambientClarityHF);
            ambientFilter.SetGain(ambientClarityLF, ambientClarityHF);

        }

        // Apply raytraced EAX results to ALReverbEffects
        CopyReverb(-1, listener.EAX, listenerReverbEffect, false);

        for (int i = 0; i < context.GroupedEAX.Count; i++)
        {
            if (groupedReverbEffects.Count <= i)
                groupedReverbEffects.Add(new());

            CopyReverb(i, context.GroupedEAX[i], groupedReverbEffects[i], true);

            groupedReverbEffects[i].Update();
        }
    }

    AnimatedFloat[] animatedRoomDiameters = new AnimatedFloat[16];
    AnimatedVector3F[] animatedEAXCenter = new AnimatedVector3F[16];

    void CopyReverb(int index, vaudio.EAXReverbResults eax, ALReverbEffect effect, bool isGroupedEAX)
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
        // TODO - why using PanAL[0]?
        if (isGroupedEAX)
        {
            // Get the difference from the camera to the reverb center
            var camera = GetViewport().GetCamera3D();
            var pos = camera.GlobalPosition;

            var cameraPosition = new vaudio.Vector3F(pos.X, pos.Y, pos.Z);

            var roomDiameterRaw = (eax.BoundsMax - eax.BoundsMin).Magnitude;

            // Animate center over 500ms to prevent stutters with moving sources with few reverb rays
            var animatedCenter = animatedEAXCenter[index];
            var animatedRoomDiameter = animatedRoomDiameters[index];

            if (animatedCenter == null)
            {
                animatedCenter = animatedEAXCenter[index] = new(eax.Center, 500, false);
                animatedRoomDiameter = animatedRoomDiameters[index] = new(roomDiameterRaw, 500, false);
            }
            else
            {
                animatedCenter.Value = eax.Center;
                animatedRoomDiameter.Value = roomDiameterRaw;
            }


            var diff = cameraPosition - animatedCenter.Value;
            var cameraDistance = diff.Magnitude;

            var roomDiameter = animatedRoomDiameter.Value;
            var roomRadius = roomDiameter * 0.23f;

            var smoothDistance = 4f;


            // Interpolate from PanAL to (0, 0, 0) when we're inside the same room
            eax.PanAL[0] = eax.PanAL[0].Normalized;

            if (cameraDistance < roomRadius)
            {
                var threshold = roomRadius - smoothDistance;
                var strength = Math.Max(0, cameraDistance - threshold) / smoothDistance;

                eax.PanAL[0] *= strength;
            }

            /*
            context.LogCallback(cameraPosition.ToString());
            context.LogCallback(eax.Center.ToString());
            context.LogCallback(animatedCenter.Value.ToString());
            context.LogCallback(roomRadius.ToString());
            context.LogCallback(cameraDistance.ToString());
            context.LogCallback(eax.PanAL[0].ToString());
            */

            // Handle normalisation failures
            if (IsNaNorInfinity(eax.PanAL[0]))
                eax.PanAL[0] = vaudio.Vector3F.Zero;
        }

        if (eax.PanAL != null)
        {
            // TODO - separate pan for late reverb and reflections
            effect.lateReverbPan[0] = eax.PanAL[0].X;
            effect.lateReverbPan[1] = eax.PanAL[0].Y;
            effect.lateReverbPan[2] = eax.PanAL[0].Z;

            effect.reflectionsPan[0] = eax.PanAL[0].X;
            effect.reflectionsPan[1] = eax.PanAL[0].Y;
            effect.reflectionsPan[2] = eax.PanAL[0].Z;
        }

        effect.dirty = true;
        effect.Update();
    }
}
