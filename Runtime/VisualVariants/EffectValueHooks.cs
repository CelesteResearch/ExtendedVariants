using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Effects.Bloom;
using Celeste.Game.Presentation.Effects.Distortion;
using Celeste.Game.Presentation.Effects.Glitch;
using Celeste.Mods.ExtendedVariants.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualVariants
{
    internal sealed class EffectValueHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();

        public EffectValueHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new ILHook(
                    typeof(BloomRendererPresentation).GetMethod(nameof(BloomRendererPresentation.Apply)), ModifyBloom));
                hooks.Add(new ILHook(typeof(OriginalGlitchRender).GetMethod(nameof(OriginalGlitchRender.Apply)),
                    ModifyGlitch));
                hooks.Add(new ILHook(
                    typeof(OriginalDistortionRender).GetMethod(nameof(OriginalDistortionRender.Render)),
                    ModifyAnxiety));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void ModifyBloom(ILContext il)
        {
            ILCursor cursor = new(il);
            int patched = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<BloomRendererPresentation>(nameof(BloomRendererPresentation.Base))))
            {
                patched++;
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.EmitDelegate<Func<float, object, float>>(GetBloomBase);
            }

            if (patched == 0) throw new InvalidOperationException("The shared bloom base read was not found.");
            patched = 0;
            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<BloomRendererPresentation>(nameof(BloomRendererPresentation
                               .Strength))))
            {
                patched++;
                cursor.Emit(OpCodes.Ldarg_2);
                cursor.EmitDelegate<Func<float, object, float>>(GetBloomStrength);
            }

            if (patched == 0) throw new InvalidOperationException("The shared bloom strength read was not found.");
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private float GetBloomBase(float original, object scene)
        {
            float value = variants.Get<float>(Variant.RoomBloom);
            return scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine) &&
                   value != -1f
                ? Math.Min(value, 1f)
                : original;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private float GetBloomStrength(float original, object scene)
        {
            float value = variants.Get<float>(Variant.RoomBloom);
            return scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine) &&
                   value != -1f
                ? Math.Max(value, 1f)
                : original;
        }

        private void ModifyGlitch(ILContext il)
        {
            ILCursor cursor = new(il);
            int patched = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<SourceEngineEffectsState>(nameof(SourceEngineEffectsState.Glitch))))
            {
                patched++;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>((original, context) =>
                    Override(original, context, Variant.GlitchEffect));
            }

            if (patched == 0) throw new InvalidOperationException("The shared glitch value read was not found.");
        }

        private void ModifyAnxiety(ILContext il)
        {
            ILCursor cursor = new(il);
            int patched = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction => instruction.MatchCallOrCallvirt<SourceEngineEffectsState>("get_Anxiety")))
            {
                patched++;
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>((original, context) =>
                    Override(original, context, Variant.AnxietyEffect));
            }

            if (patched == 0) throw new InvalidOperationException("The shared anxiety value read was not found.");
        }

        private float Override(float original, ISourceEngineContext context, Variant variant)
        {
            if (!ReferenceEquals(context, engine)) return original;
            float value = variants.Get<float>(variant);
            return value == -1f ? original : value;
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
