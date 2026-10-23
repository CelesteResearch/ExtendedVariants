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
    // legacy extended variant trigger that can set any variant by taking an integer as an input.
    // this used tricks such as multiplying by 10 for multipliers, using a list for color grades, or taking a bit field as an input for dash directions.
    // ... it was bad, and is obsoleted by the new typed extended variant triggers.
    [CustomEntity("ExtendedVariantTrigger", "ExtendedVariantMode/ExtendedVariantTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.Legacy.ExtendedVariantTrigger")]
    public class ExtendedVariantTrigger : SourceTriggerEntityState
    {
        protected readonly VariantRuntime runtime;

        protected void Apply(Variant variant, object value, bool revertOnLeave, bool isFade, bool revertOnDeath,
            bool legacy)
            => runtime.Enter(variant, value, revertOnLeave, revertOnDeath, legacy);


        // === extended variant trigger code

        private Variant variantChange;
        private object newValue;
        private bool revertOnLeave;
        private bool revertOnDeath;
        private bool withTeleport;
        private bool isLegacy = true;

        public ExtendedVariantTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = VariantRuntime.For(engine);
            // parse the trigger parameters
            variantChange = data.Enum("variantChange", Variant.Gravity);
            newValue = data.Int("newValue", 10);
            revertOnLeave = data.Bool("revertOnLeave", false);
            revertOnDeath = data.Bool("revertOnDeath", true);
            withTeleport = data.Bool("withTeleport", false);

            if (!data.Bool("enable", true))
            {
                // "disabling" a variant is actually just resetting its value to default
                isLegacy = false;
                newValue = VariantDefaults.Get(variantChange);
            }
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);

            Action applyVariant = () =>
                Apply(variantChange, newValue, revertOnLeave, isFade: false, revertOnDeath, isLegacy);

            if (withTeleport)
            {
                runtime.QueueTeleport(applyVariant);
            }
            else
            {
                applyVariant();
            }
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);

            if (revertOnLeave)
            {
                runtime.Leave(variantChange, newValue, isLegacy);
            }
        }
    }
}
