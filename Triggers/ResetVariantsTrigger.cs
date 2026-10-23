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
    [CustomEntity("ExtendedVariantMode/ResetVariantsTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.ResetVariantsTrigger")]
    public class ResetVariantsTrigger : SourceTriggerEntityState
    {
        protected readonly VariantRuntime runtime;

        protected void Apply(Variant variant, object value, bool revertOnLeave, bool isFade, bool revertOnDeath,
            bool legacy)
            => runtime.Enter(variant, value, revertOnLeave, revertOnDeath, legacy);

        private readonly bool vanilla;
        private readonly bool extended;

        public ResetVariantsTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = VariantRuntime.For(engine);
            vanilla = data.Bool("vanilla");
            extended = data.Bool("extended");
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            if (vanilla)
            {
                runtime.Reset(vanilla: true);
            }

            if (extended)
            {
                runtime.Reset(vanilla: false);
            }
        }
    }
}
