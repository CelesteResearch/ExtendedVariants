using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Framework.Components;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Engine.Actions;
using Celeste.Game.Execution.Engine.Update;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.TimingVariants
{
    public sealed partial class TimingVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private float previousGameSpeed = 1f;
        private bool disposed;

        private TimingVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                InstallGameSpeed();
                hooks.Add(new Hook(typeof(OriginalEngineFrame).GetMethod(nameof(OriginalEngineFrame.Update)),
                    (Action<Action<OriginalEngineFrame, SourceGameTime>, OriginalEngineFrame, SourceGameTime>)
                    UpdateEngine));
                using (new DetourConfigContext(
                           new DetourConfig("ExtendedVariantMode_BeforeAll").WithPriority(int.MinValue)).Use())
                    hooks.Add(new Hook(typeof(OriginalCelesteFreeze).GetMethod(nameof(OriginalCelesteFreeze.Freeze)),
                        (Action<Action<ISourceEngineContext, float>, ISourceEngineContext, float>)Freeze));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new TimingVariantProcess(engine);

        private void Freeze(Action<ISourceEngineContext, float> original, ISourceEngineContext context, float time)
        {
            if (!ReferenceEquals(context, engine) || !variants.Get<bool>(Variant.NoFreezeFrames))
            {
                original(context, time);
                return;
            }

            if (variants.Get<bool>(Variant.NoFreezeFramesAdvanceCassetteBlocks)) AdvanceCassette(time);
        }

        private void UpdateEngine(Action<OriginalEngineFrame, SourceGameTime> original, OriginalEngineFrame frame,
            SourceGameTime time)
        {
            if (ReferenceEquals(frame.State, engine.Frame) && variants.Get<bool>(Variant.NoFreezeFrames) &&
                engine.Effects.FreezeTimer > 0f)
            {
                if (variants.Get<bool>(Variant.NoFreezeFramesAdvanceCassetteBlocks))
                    AdvanceCassette(engine.Effects.FreezeTimer);
                engine.Effects.FreezeTimer = 0f;
            }

            original(frame, time);
        }

        private void AdvanceCassette(float time)
        {
            if (engine.Frame.Scenes.CurrentScene is not ISourceScene scene) return;
            var managers = scene.SourceScene.Entities.GetTrackedEntities("Celeste.CassetteBlockManager");
            if (managers.Count > 0) ((IOriginalCassetteMusicAdvance)managers[0]).AdvanceMusic(time);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            previousGameSpeed = 10f;
        }
    }
}
