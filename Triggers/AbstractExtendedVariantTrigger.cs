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
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Mods.ExtendedVariants.Components;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    public abstract class AbstractExtendedVariantTrigger<T> : SourceTriggerEntityState
    {
        protected readonly VariantRuntime runtime;

        protected void Apply(Variant variant, object value, bool revertOnLeave, bool isFade, bool revertOnDeath,
            bool legacy)
            => runtime.Enter(variant, value, revertOnLeave, revertOnDeath, legacy);

        private Variant variantChange;
        private Func<T> newValueGetter;
        private T valueToRevertOnLeave;
        private bool revertOnLeave;
        private bool revertOnDeath;
        private bool delayRevertOnDeath;
        private bool withTeleport;
        private bool coversScreen;
        private bool onlyOnce;

        public AbstractExtendedVariantTrigger(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
            data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            runtime = VariantRuntime.For(engine);
            // parse the trigger parameters
            variantChange = getVariant(data);
            newValueGetter = () => getNewValue(data);
            revertOnLeave = data.Bool("revertOnLeave", false);
            revertOnDeath = data.Bool("revertOnDeath", true);
            delayRevertOnDeath = data.Bool("delayRevertOnDeath", false);
            withTeleport = data.Bool("withTeleport", false);
            coversScreen = data.Bool("coversScreen", false);
            onlyOnce = data.Bool("onlyOnce", false);

            if (!data.Bool("enable", true))
            {
                // "disabling" a variant is actually just resetting its value to default
                newValueGetter = () => (T)VariantDefaults.Get(variantChange);
            }

            valueToRevertOnLeave = newValueGetter();

            // this is a replacement for the Flag-Toggled Extended Variant Trigger.
            if (!string.IsNullOrEmpty(data.Attr("flag")))
            {
                Add(new FlagToggleComponent(data.Attr("flag"), data.Bool("flagInverted")));
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);

            var bounds = ((OriginalLevelSceneRoot)scene).Bounds;

            // this is a replacement for the Extended Variant Controller.
            if (coversScreen)
            {
                // the trigger should stick out on the top because the player can go offscreen by up to 24px when there is no screen above.
                Position = new Vector2(bounds.X, bounds.Y - 24f);
                Collider.Width = bounds.Width;
                Collider.Height = bounds.Height + 32f;
            }
        }

        protected virtual Variant getVariant(EntityData data)
        {
            return data.Enum("variantChange", Variant.Gravity);
        }

        protected abstract T getNewValue(EntityData data);

        public override void OnEnter(RoomPlayerSession player)
        {
            base.OnEnter(player);

            Action applyVariant = () =>
            {
                T value = newValueGetter();
                Apply(variantChange, value, revertOnLeave, isFade: false, revertOnDeath, legacy: false);
                valueToRevertOnLeave = value;
            };

            if (withTeleport)
            {
                runtime.QueueTeleport(applyVariant);
            }
            else
            {
                applyVariant();
            }

            if (onlyOnce)
            {
                RemoveSelf();
            }
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);

            if (revertOnLeave && (!delayRevertOnDeath || !player.Dead))
            {
                runtime.Leave(variantChange, valueToRevertOnLeave, legacy: false);
            }
        }
    }
}
