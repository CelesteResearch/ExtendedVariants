using System;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators;
using Celeste.Simulation.Gameplay.Player.Intro;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud
{
    public sealed class JumpIndicatorProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly IDisposable hook;
        private readonly Action<SceneEntityState> tieToPlayer;

        private JumpIndicatorProcess(ISourceEngineContext engine, Action<SceneEntityState> tieToPlayer)
        {
            this.engine = engine;
            this.tieToPlayer = tieToPlayer;
            hook = new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot, PlayerIntroType,
                    bool>)LoadLevel);
            try
            {
                if (engine.Frame.Scenes.CurrentScene is OriginalLevelSceneRoot level)
                    AddIndicator(level);
            }
            catch
            {
                hook.Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine, Action<SceneEntityState> tieToPlayer = null)
            => new JumpIndicatorProcess(engine, tieToPlayer);

        private void LoadLevel(Action<OriginalLevelSceneRoot, PlayerIntroType, bool> original,
            OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            original(level, intro, fromLoader);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            if (intro != PlayerIntroType.Transition) JumpVariantProcess.For(engine).Refill();
            if (level.SourceScene.Entities.GetTrackedEntities(typeof(JumpIndicator)).Count == 0) AddIndicator(level);
        }

        private void AddIndicator(OriginalLevelSceneRoot level)
        {
            level.SourceScene.AddEntity(new JumpIndicator(engine, tieToPlayer));
            level.SourceScene.CommitQueuedEntityLists();
        }

        public void Dispose() => hook.Dispose();
    }
}
