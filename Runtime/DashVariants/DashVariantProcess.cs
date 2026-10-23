using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Input.Gameplay;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Player.Configuration;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Presentation.Hair;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.Gameplay.Player.Runtime;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.DashVariants
{
    public sealed partial class DashVariantProcess : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private readonly ConditionalWeakTable<DashSequence, DashState> dashOwners = new();
        private readonly ConditionalWeakTable<PlayerBoostSequence, DashState> boostOwners = new();
        private readonly ConditionalWeakTable<PlayerFrameMotion, DashState> frameOwners = new();
        private readonly ConditionalWeakTable<PlayerHairAppearance, DashState> hairOwners = new();

        private static readonly FieldInfo BoostSequenceField =
            typeof(RoomPlayerSession).GetField("boostSequence", BindingFlags.Instance | BindingFlags.NonPublic);

        private sealed class DashState
        {
            public RoomPlayerSession Player;
            public int CountBeforeDash;
            public Vector2 DirectionBeforeDash;
        }

        private delegate void PlayerConstructor(RoomPlayerSession player, Vector2 spawn, IPlayerAssistSettings settings,
            ISourceGameInput input, ISourceEngineContext engine, PlatformCollisionWorld world,
            IReadOnlyDictionary<string, string> chaseSoundMapping, PlayerSpriteMode spriteMode);

        private DashVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
            try
            {
                var constructor = typeof(RoomPlayerSession).GetConstructor(new[]
                {
                    typeof(Vector2), typeof(IPlayerAssistSettings),
                    typeof(ISourceGameInput), typeof(ISourceEngineContext), typeof(PlatformCollisionWorld),
                    typeof(IReadOnlyDictionary<string, string>), typeof(PlayerSpriteMode)
                });
                hooks.Add(new Hook(constructor,
                    (Action<PlayerConstructor, RoomPlayerSession, Vector2, IPlayerAssistSettings, ISourceGameInput,
                        ISourceEngineContext, PlatformCollisionWorld, IReadOnlyDictionary<string, string>,
                        PlayerSpriteMode>)
                    ((orig, player, spawn, settings, input, context, world, mapping, sprite) =>
                    {
                        orig(player, spawn, settings, input, context, world, mapping, sprite);
                        if (ReferenceEquals(context, engine)) Register(player);
                    })));
                if (engine.Frame.Scenes.CurrentScene is ISourceScene scene)
                    foreach (var player in scene.SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession)))
                        Register((RoomPlayerSession)player);
                InstallDashCount();
                InstallDashDirection();
                InstallGroundRefills();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new DashVariantProcess(engine);

        private void Register(RoomPlayerSession player)
        {
            var state = dashOwners.GetValue(player.Motor.Dash, _ => new DashState { Player = player });
            boostOwners.GetValue((PlayerBoostSequence)BoostSequenceField.GetValue(player), _ => state);
            frameOwners.GetValue(player.Motor.BeforeState, _ => state);
            hairOwners.GetValue(player.Appearance, _ => state);
        }

        private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
                                                                        BindingFlags.Instance | BindingFlags.Public |
                                                                        BindingFlags.NonPublic |
                                                                        BindingFlags.DeclaredOnly)
                                                                    ?? throw new MissingMethodException(type.FullName,
                                                                        name);

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
