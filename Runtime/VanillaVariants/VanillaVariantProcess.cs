using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Runtime.Events.Player;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Save.Data;
using Celeste.Mods.ExtendedVariants.Configuration;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VanillaVariants
{
    public sealed class VanillaVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly VanillaAssistState state;
        private readonly IDisposable update;
        private readonly IDisposable render;

        private VanillaVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            state = VanillaAssistState.For(engine);
            update = new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Update)),
                (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)Invoke);
            try
            {
                render = new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Render)),
                    (Action<Action<OriginalLevelSceneRoot>, OriginalLevelSceneRoot>)Invoke);
                EverestPlayerSourceEvents.OnRegisterStates += state.RecordPlayer;
            }
            catch
            {
                update.Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new VanillaVariantProcess(engine);

        private void Invoke(Action<OriginalLevelSceneRoot> original, OriginalLevelSceneRoot level)
        {
            if (!ReferenceEquals(level.EngineContext, engine))
            {
                original(level);
                return;
            }

            OriginalSaveDataState save = ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave;
            FileAssists previous = save.Assists;
            state.BeginScope(previous);
            FileAssists current = VanillaAssistState.ApplyMap(previous, variants, out bool changed);
            if (changed) save.Assists = current;
            try
            {
                original(level);
            }
            finally
            {
                FileAssists restored = state.EndScope();
                if (changed) save.Assists = restored;
            }
        }

        public void Dispose()
        {
            EverestPlayerSourceEvents.OnRegisterStates -= state.RecordPlayer;
            render.Dispose();
            update.Dispose();
        }
    }
}
