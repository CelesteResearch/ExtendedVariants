using Celeste.Core.Reflection.Runtime;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/SetJumpCountTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.SetJumpCountTrigger")]
    public sealed class SetJumpCountTrigger : SourceTriggerEntityState
    {
        private enum Mode
        {
            Cap,
            Set
        }

        private readonly int jumpCount;
        private readonly Mode mode;
        private readonly ISourceEngineContext engine;

        public SetJumpCountTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            this.engine = engine;
            jumpCount = data.Int("jumpCount");
            mode = data.Enum("mode", Mode.Set);
        }

        public override void OnEnter(RoomPlayerSession player) =>
            JumpVariantProcess.For(engine).Set(jumpCount, mode == Mode.Cap);
    }
}
