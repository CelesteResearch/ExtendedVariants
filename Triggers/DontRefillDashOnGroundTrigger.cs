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
    [CustomEntity("ExtendedVariantMode/DontRefillDashOnGroundTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.DontRefillDashOnGroundTrigger")]
    public class DontRefillDashOnGroundTrigger : AbstractExtendedVariantTrigger<DashRefillOnGroundConfiguration>
    {
        public DontRefillDashOnGroundTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            engine, data, offset)
        {
        }

        protected override Variant getVariant(EntityData data)
        {
            return Variant.DontRefillDashOnGround;
        }

        protected override DashRefillOnGroundConfiguration getNewValue(EntityData data)
        {
            return data.Enum("newValue", DashRefillOnGroundConfiguration.DEFAULT);
        }
    }
}
