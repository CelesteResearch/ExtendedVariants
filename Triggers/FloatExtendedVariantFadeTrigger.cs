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
    [CustomEntity("ExtendedVariantMode/FloatExtendedVariantFadeTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.FloatExtendedVariantFadeTrigger")]
    public class FloatExtendedVariantFadeTrigger : SourceTriggerEntityState
    {
        protected readonly VariantRuntime runtime;

        protected void Apply(Variant variant, object value, bool revertOnLeave, bool isFade, bool revertOnDeath,
            bool legacy)
            => runtime.Enter(variant, value, revertOnLeave, revertOnDeath, legacy);

        private readonly Variant variantChange;
        private readonly float valueA;
        private readonly float valueB;
        private readonly PositionModes positionMode;
        private readonly bool revertOnDeath;

        public FloatExtendedVariantFadeTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = VariantRuntime.For(engine);
            variantChange = data.Enum("variantChange", Variant.Gravity);
            valueA = data.Float("valueA", 1f);
            valueB = data.Float("valueB", 1f);
            positionMode = data.Enum("positionMode", PositionModes.LeftToRight);
            revertOnDeath = data.Bool("revertOnDeath", true);
        }

        public override void OnStay(RoomPlayerSession player)
        {
            base.OnStay(player);

            float targetValue = ScalarMath.ClampedMap(GetPositionLerp(player, positionMode), 0, 1, valueA, valueB);
            Apply(variantChange, targetValue, revertOnLeave: false, isFade: true, revertOnDeath, legacy: false);
        }
    }
}
