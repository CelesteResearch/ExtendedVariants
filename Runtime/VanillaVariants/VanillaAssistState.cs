using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Menus.Overworld;
using Celeste.Game.Save.Data;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Mods.ExtendedVariants.Persistence;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Configuration;
using Celeste.Scene.Tracking;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;

namespace Celeste.Mods.ExtendedVariants.Runtime.VanillaVariants
{
    public sealed class VanillaAssistState
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, VanillaAssistState> States = new();

        public static readonly Variant[] OrderedVariants =
        {
            Variant.AirDashes, Variant.DashAssist, Variant.VanillaGameSpeed, Variant.Hiccups,
            Variant.InfiniteStamina, Variant.Invincible, Variant.InvisibleMotion, Variant.LowFriction,
            Variant.MirrorMode, Variant.NoGrabbing, Variant.PlayAsBadeline, Variant.SuperDashing,
            Variant.ThreeSixtyDashing
        };

        private static readonly object FalseValue = false;
        private static readonly object NormalDashes = DashMode.Normal;
        private static readonly object DefaultSpeed = 10;
        private readonly ISourceEngineContext engine;
        private FileAssists baseline;
        private bool inScope;
        private RoomPlayerSession latestPlayer;
        private VanillaAssistState(ISourceEngineContext engine) => this.engine = engine;

        public static VanillaAssistState For(ISourceEngineContext engine) =>
            States.GetValue(engine, static e => new(e));

        private ref FileAssists Assists => ref ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists;

        public void BeginScope(FileAssists previous)
        {
            baseline = previous;
            inScope = true;
        }

        public FileAssists EndScope()
        {
            inScope = false;
            return baseline;
        }

        public void BeginPlayerChanges()
        {
            if (!inScope) baseline = Assists;
        }

        public void RecordPlayer(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, engine)) latestPlayer = player;
        }

        public static object Default(Variant variant) => variant == Variant.AirDashes ? NormalDashes :
            variant == Variant.VanillaGameSpeed ? DefaultSpeed : FalseValue;

        public static object Current(VariantRuntime runtime, Variant variant, bool mapOnly)
        {
            VariantSession session = runtime.Session;
            if ((mapOnly || !session.VariantsOverridenByUser.Contains(variant)) &&
                runtime.Engine.Frame.Scenes.CurrentScene is not OverworldSequence)
            {
                if (session.RevertOnLeaveVariantsActive.TryGetValue(variant, out var leave))
                    return leave[leave.Count - 1];
                if (session.OverriddenVariantsInRoom.TryGetValue(variant, out var room)) return room;
                if (session.VariantsEnabledViaTrigger.TryGetValue(variant, out var value)) return value;
            }

            return !mapOnly && runtime.GetSettings().EnabledVariants.TryGetValue(variant, out var setting)
                ? setting
                : Default(variant);
        }

        public static FileAssists Apply(FileAssists target, Variant variant, object value)
        {
            switch (variant)
            {
                case Variant.AirDashes: target.DashMode = (DashMode)value; break;
                case Variant.VanillaGameSpeed: target.GameSpeed = (int)value; break;
                case Variant.DashAssist: target.DashAssist = (bool)value; break;
                case Variant.Hiccups: target.Hiccups = (bool)value; break;
                case Variant.InfiniteStamina: target.InfiniteStamina = (bool)value; break;
                case Variant.Invincible: target.Invincible = (bool)value; break;
                case Variant.InvisibleMotion: target.InvisibleMotion = (bool)value; break;
                case Variant.LowFriction: target.LowFriction = (bool)value; break;
                case Variant.MirrorMode: target.MirrorMode = (bool)value; break;
                case Variant.NoGrabbing: target.NoGrabbing = (bool)value; break;
                case Variant.PlayAsBadeline: target.PlayAsBadeline = (bool)value; break;
                case Variant.SuperDashing: target.SuperDashing = (bool)value; break;
                case Variant.ThreeSixtyDashing: target.ThreeSixtyDashing = (bool)value; break;
            }

            return target;
        }

        public static FileAssists ApplyMap(FileAssists target, VariantRuntime runtime, out bool changed)
        {
            changed = false;
            for (int i = 0; i < OrderedVariants.Length; i++)
            {
                Variant variant = OrderedVariants[i];
                object value = Current(runtime, variant, true);
                if (!runtime.Session.VariantsOverridenByUser.Contains(variant) &&
                    !VariantRuntime.Identical(value, Default(variant)))
                {
                    target = Apply(target, variant, value);
                    changed = true;
                }
            }

            return target;
        }

        public void ChangedByPlayer(Variant variant, object value)
        {
            baseline = Apply(baseline, variant, value);
            Assists = Apply(Assists, variant, value);
        }

        public void ValueChanged(VariantRuntime runtime, Variant variant)
        {
            var active = ApplyMap(baseline, runtime, out _);
            RoomPlayerSession player = (engine.Frame.Scenes.CurrentScene as ISourceScene)?.SourceScene.Entities
                .GetTrackedEntity<RoomPlayerSession>(typeof(RoomPlayerSession).FullName);
            switch (variant)
            {
                case Variant.AirDashes:
                    if (player != null)
                        player.Data.Dashes = Math.Min(player.Data.Dashes,
                            active.DashMode != DashMode.Normal ? 2 : player.Motor.BeforeState.MaxDashes);
                    break;
                case Variant.VanillaGameSpeed:
                    engine.Effects.TimeRateB = active.GameSpeed / 10f;
                    break;
                case Variant.MirrorMode:
                    var input = ((IOriginalGameInputProcessEnvironment)engine).InputSource;
                    input.MoveX.Inverted = input.Aim.InvertedX = input.Feather.InvertedX = active.MirrorMode;
                    break;
                case Variant.PlayAsBadeline:
                    player ??= latestPlayer;
                    if (player != null)
                    {
                        var mode = active.PlayAsBadeline
                            ? PlayerSpriteMode.MadelineAsBadeline
                            : player.DefaultSpriteMode;
                        if (player.Active) player.ResetSpriteNextFrame(mode);
                        else player.ResetSprite(mode);
                    }

                    break;
            }
        }
    }
}
