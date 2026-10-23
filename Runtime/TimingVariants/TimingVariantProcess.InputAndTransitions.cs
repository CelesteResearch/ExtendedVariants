using System;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Levels.Transitions;
using Celeste.Game.Execution.Levels.Transitions;
using Celeste.Game.Execution.Levels;
using Celeste.Input.Bindings;
using Celeste.Input.Runtime;
using Celeste.Mods.ExtendedVariants.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.TimingVariants
{
    public sealed partial class TimingVariantProcess
    {
        private static readonly Func<SourceVirtualInput, SourceMInputProcess> InputProcess =
            (Func<SourceVirtualInput, SourceMInputProcess>)typeof(SourceVirtualInput)
                .GetProperty("Process", BindingFlags.Instance | BindingFlags.NonPublic).GetGetMethod(true)
                .CreateDelegate(typeof(Func<SourceVirtualInput, SourceMInputProcess>));

        private void InstallRepeatTimer()
        {
            hooks.Add(new ILHook(typeof(SourceVirtualButton).GetMethod(nameof(SourceVirtualButton.Update)),
                ModifySourceRepeat));
        }

        private void ModifySourceRepeat(ILContext il)
        {
            ILCursor cursor = new(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<SourceVirtualButton>("repeatCounter")))
                throw new InvalidOperationException("The actual source button repeat counter was not found.");
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchCallOrCallvirt<ISourceMInputEngine>("get_DeltaTime")))
                throw new InvalidOperationException("The actual source button repeat time was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, SourceVirtualButton, float>>((value, button) =>
                ReferenceEquals(InputProcess(button), ((ISourceMInputEnvironment)engine).MInput)
                    ? RepeatDelta(value)
                    : value);
        }

        private float RepeatDelta(float original)
        {
            float rate = engine.Effects.EffectiveTimeRate;
            return rate < .5f ? engine.RawDeltaTime * .5f : rate > 1.6f ? engine.RawDeltaTime * 1.6f : original;
        }

        private void InstallAntiSoftlock()
        {
            Type routine =
                typeof(EverestLevelTransitionPolicy).GetNestedType("TransitionRoutine", BindingFlags.NonPublic);
            FieldInfo sequence = routine.GetField("sequence", BindingFlags.Instance | BindingFlags.NonPublic);
            hooks.Add(new ILHook(routine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public), il =>
            {
                ILCursor cursor = new(il);
                int patched = 0;
                while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR8(5d)))
                {
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate<Func<double, object, double>>((original, current) =>
                    {
                        OriginalLevelSceneRoot level = ((OriginalLevelTransitionSequence)sequence.GetValue(current))
                            .Level;
                        if (!ReferenceEquals(level.EngineContext, engine)) return original;
                        float speed = variants.Get<float>(Variant.GameSpeed);
                        if (speed <= 0f || engine.Frame.Scenes.CurrentScene is not OriginalLevelSceneRoot active)
                            return original;
                        float duration = active.NextTransitionDuration / speed + 2f;
                        return duration > original ? duration : original;
                    });
                    patched++;
                }

                if (patched == 0)
                    throw new InvalidOperationException("The actual Everest transition timeout was not found.");
            }));
        }
    }
}
