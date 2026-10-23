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
    [CustomEntity("ExtendedVariantMode/BooleanExtendedVariantTrigger",
        "ExtendedVariantMode/BooleanVanillaVariantTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.BooleanExtendedVariantTrigger")]
    public class BooleanExtendedVariantTrigger : AbstractExtendedVariantTrigger<bool>
    {
        private readonly Variant variantChange;
        private readonly bool toggle;

        public BooleanExtendedVariantTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            engine, data, offset)
        {
            variantChange = getVariant(data);
            toggle = data.Bool("toggle");
        }

        protected override bool getNewValue(EntityData data)
        {
            if (toggle)
            {
                return !(bool)runtime.GetCurrent(variantChange);
            }

            return data.Bool("newValue");
        }
    }
}
