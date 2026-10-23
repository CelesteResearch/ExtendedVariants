using System;
using System.Linq;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.VisualHud
{
    public sealed partial class VisualHudProcess
    {
        partial void InstallIndicators()
        {
            hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                (Action<Action<OriginalLevelSceneRoot, PlayerIntroType, bool>, OriginalLevelSceneRoot, PlayerIntroType,
                    bool>)LoadIndicators));
        }

        private void LoadIndicators(Action<OriginalLevelSceneRoot, PlayerIntroType, bool> original,
            OriginalLevelSceneRoot level, PlayerIntroType intro, bool fromLoader)
        {
            original(level, intro, fromLoader);
            if (!ReferenceEquals(level.EngineContext, engine)) return;
            var scene = level.SourceScene;
            if (!scene.Entities.Any(entity => entity is DashCountIndicator && entity is not Speedometer))
            {
                scene.AddEntity(new DashCountIndicator(engine));
                scene.CommitQueuedEntityLists();
            }

            if (!scene.Entities.Any(entity => entity is Speedometer))
            {
                scene.AddEntity(new Speedometer(engine));
                scene.CommitQueuedEntityLists();
            }
        }
    }
}
