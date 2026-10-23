using System.Collections;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Core.Components;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Everest.Gameplay.Environment.Water;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.ExtendedVariants.Runtime.Water
{
    [RuntimeTypeAlias("ExtendedVariants.Entities.UnderwaterSwitchController")]
    public class UnderwaterSwitchController : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private OriginalWaterState water;

        public UnderwaterSwitchController(ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            Add(new SimulationCoroutine(engine, Routine()));
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value);
        }

        public IEnumerator Routine()
        {
            var session = ((OriginalLevelSceneRoot)Scene).Session;
            while (true)
            {
                if (!variants.Get<bool>(Variant.EverythingIsUnderwater))
                {
                    yield return null;
                    continue;
                }

                if (water == null) SpawnWater(((OriginalLevelRoomData)session.LevelData).Bounds);
                while (variants.Get<bool>(Variant.EverythingIsUnderwater) && variants.MasterSwitch)
                    yield return null;
                ((OriginalLevelSceneRoot)Scene).RemoveEntity(water);
                water = null;
                if (!variants.MasterSwitch) break;
            }

            RemoveSelf();
        }

        private void SpawnWater(IntRect bounds)
        {
            water = new EverestWaterState(engine, new Vector2(bounds.Left, bounds.Top - 10), false, false,
                bounds.Width, bounds.Height + 10);
            IntRect fill = WaterFillAccess.Read(water);
            fill.Y += 10;
            fill.Height -= 10;
            WaterFillAccess.Write(water, fill);
            ((OriginalLevelSceneRoot)Scene).AddEntity(water);
        }
    }
}
