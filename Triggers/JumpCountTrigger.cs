using Celeste.Core.Reflection.Runtime;
using System;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Simulation;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/JumpCountTrigger")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.ForMappers.JumpCountTrigger")]
    public sealed class JumpCountTrigger : IntegerExtendedVariantTrigger
    {
        private readonly bool capOnChange;
        private readonly ISourceEngineContext engine;

        public JumpCountTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(engine, data,
            offset)
        {
            this.engine = engine;
            capOnChange = data.Bool("capOnChange");
        }

        public override void OnEnter(RoomPlayerSession player) => CheckCap(() => base.OnEnter(player));
        public override void OnLeave(RoomPlayerSession player) => CheckCap(() => base.OnLeave(player));

        private void CheckCap(Action action)
        {
            if (!capOnChange)
            {
                action();
                return;
            }

            int before = runtime.Get<int>(Variant.JumpCount);
            action();
            int after = runtime.Get<int>(Variant.JumpCount);
            if (before != after) JumpVariantProcess.For(engine).Set(Math.Max(0, after - 1), true);
        }

        protected override int getNewValue(EntityData data) =>
            data.Bool("infinite") ? int.MaxValue : base.getNewValue(data);
    }
}
