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
    // extended variant trigger that can fade _any_ variant (yes, even Color Grading) by fading its legacy integer value.
    // obsoleted by Float Extended Variant Fade Trigger, that fades float variants way more smoothly (that is, not by 0.1x / 10% steps).
    [CustomEntity("ExtendedVariantMode/ExtendedVariantFadeTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.Legacy.ExtendedVariantFadeTrigger")]
    public class ExtendedVariantFadeTrigger : SourceTriggerEntityState
    {
        protected readonly VariantRuntime runtime;

        protected void Apply(Variant variant, object value, bool revertOnLeave, bool isFade, bool revertOnDeath,
            bool legacy)
            => runtime.Enter(variant, value, revertOnLeave, revertOnDeath, legacy);

        private readonly Variant variantChange;
        private readonly int valueA;
        private readonly int valueB;
        private readonly PositionModes positionMode;
        private readonly bool revertOnDeath;

        public ExtendedVariantFadeTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = VariantRuntime.For(engine);
            variantChange = data.Enum("variantChange", Variant.Gravity);
            valueA = data.Int("valueA", 10);
            valueB = data.Int("valueB", 10);
            positionMode = data.Enum("positionMode", PositionModes.LeftToRight);
            revertOnDeath = data.Bool("revertOnDeath", true);
        }

        public override void OnStay(RoomPlayerSession player)
        {
            base.OnStay(player);

            float targetValue = ScalarMath.ClampedMap(GetPositionLerp(player, positionMode), 0, 1, valueA, valueB);
            Apply(variantChange, (int)Math.Round(targetValue), revertOnLeave: false, isFade: true, revertOnDeath,
                legacy: true);
        }
    }
}
