using Celeste.Core.Reflection.Runtime;
using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Mods.ExtendedVariants.Components;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/DashDirectionTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.DashDirectionTrigger")]
    public class DashDirectionTrigger : AbstractExtendedVariantTrigger<bool[][]>
    {
        public DashDirectionTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine, data,
            offset)
        {
        }

        protected override Variant getVariant(EntityData data)
        {
            return Variant.DashDirection;
        }

        protected override bool[][] getNewValue(EntityData data)
        {
            return new bool[][]
            {
                new bool[] { data.Bool("topLeft"), data.Bool("top"), data.Bool("topRight") },
                new bool[] { data.Bool("left"), true, data.Bool("right") },
                new bool[] { data.Bool("bottomLeft"), data.Bool("bottom"), data.Bool("bottomRight") },
            };
        }
    }
}
