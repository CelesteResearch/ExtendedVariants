using Celeste.Core.Reflection.Runtime;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/ExtendedVariantOnScreenDisplayTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.ExtendedVariantOnScreenDisplayTrigger")]
    public sealed class ExtendedVariantOnScreenDisplayTrigger : SourceTriggerEntityState
    {
        private readonly bool enable;
        private readonly VariantRuntime runtime;

        public ExtendedVariantOnScreenDisplayTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            enable = data.Bool("enable");
            runtime = VariantRuntime.For(engine);
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);
            runtime.Session.ExtendedVariantsDisplayedOnScreenViaTrigger = enable;
        }
    }
}
