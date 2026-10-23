// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Collectibles.SummitGems;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Collision;
using Celeste.Simulation.Gameplay.Player.Audio.Frame;
using Celeste.Simulation.Gameplay.Player.Configuration;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.ExtendedVariants.Runtime.StaminaAndDash
{
    internal sealed class StaminaVariantHooks : IDisposable
    {
        private static readonly string sourcePlayerRegistryKey =
            typeof(global::Celeste.Simulation.RoomPlayerSession).FullName;

        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime runtime;
        private readonly List<IDisposable> hooks = new();
        private bool forceRefill;

        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private delegate void PlayerConstructor(RoomPlayerSession player, Vector2 spawn, IPlayerAssistSettings settings,
            Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext engine,
            PlatformCollisionWorld world, IReadOnlyDictionary<string, string> sounds, PlayerSpriteMode mode);

        private delegate void ConstructorHook(PlayerConstructor original, RoomPlayerSession player, Vector2 spawn,
            IPlayerAssistSettings settings, Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext engine,
            PlatformCollisionWorld world, IReadOnlyDictionary<string, string> sounds, PlayerSpriteMode mode);

        internal StaminaVariantHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtime = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new ILHook(typeof(ClimbSequence).GetMethod(nameof(ClimbSequence.Update)),
                    il => PatchEngineConstant(il, typeof(ClimbSequence).GetField("engine", InstanceMembers))));
                hooks.Add(new ILHook(typeof(SwimSequence).GetMethod(nameof(SwimSequence.Begin)),
                    il => PatchEngineConstant(il, typeof(SwimSequence).GetField("engine", InstanceMembers))));
                hooks.Add(new ILHook(PlayerMethod("DreamDashBegin"), PatchPlayerConstant));
                hooks.Add(new ILHook(typeof(PlayerFrameMotion).GetMethod(nameof(PlayerFrameMotion.Update)),
                    PatchFrameConstants));
                var summitMethod = typeof(OriginalSummitGemState).GetMethod("SmashRoutine", InstanceMembers);
                hooks.Add(new ILHook(summitMethod.GetStateMachineTarget(), il => PatchIteratorEngineConstant(il,
                    typeof(OriginalSummitGemState).GetField("engine", InstanceMembers))));
                var constructor = typeof(RoomPlayerSession).GetConstructors()[0];
                hooks.Add(new ILHook(constructor, PatchPlayerInitialization));
                hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.RefillStamina)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)RefillStamina));
                var priority = new DetourConfig("ExtendedVariantMode_AfterAll", priority: int.MaxValue);
                hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.OnTransition)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)OnTransition, priority));
                hooks.Add(new Hook(constructor, (ConstructorHook)ConstructPlayer, priority));
                hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.UseRefill)),
                    (Func<Func<RoomPlayerSession, bool, bool>, RoomPlayerSession, bool, bool>)UseRefill, priority));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private static MethodInfo PlayerMethod(string name) =>
            typeof(RoomPlayerSession).GetMethod(name, InstanceMembers)
            ?? throw new MissingMethodException(typeof(RoomPlayerSession).FullName, name);

        private void PatchEngineConstant(ILContext il, FieldInfo engineField)
        {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(110f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, engineField);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>(StaminaAmount);
            }
        }

        private void PatchPlayerConstant(ILContext il)
        {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(110f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Call,
                    typeof(RoomPlayerSession).GetProperty(nameof(RoomPlayerSession.EngineContext)).GetMethod);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>(StaminaAmount);
            }
        }

        private void PatchFrameConstants(ILContext il)
        {
            var cursor = new ILCursor(il);
            var worldField = typeof(PlayerFrameMotion).GetField("world", InstanceMembers);
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(110f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, worldField);
                cursor.EmitDelegate<Func<float, IPlayerFrameWorld, float>>((original, world) =>
                    world is RoomPlayerWorld actual ? StaminaAmount(original, actual.EngineContext) : original);
            }
        }

        private void PatchIteratorEngineConstant(ILContext il, FieldInfo engineField)
        {
            var cursor = new ILCursor(il);
            // Reflection identity comes from the fixed iterator's state-machine attribute.
            var stateType = typeof(OriginalSummitGemState).GetMethod("SmashRoutine", InstanceMembers)
                .GetCustomAttribute<System.Runtime.CompilerServices.IteratorStateMachineAttribute>().StateMachineType;
            var ownerField = stateType.GetField("<>4__this", InstanceMembers);
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(110f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, ownerField);
                cursor.Emit(OpCodes.Ldfld, engineField);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>(StaminaAmount);
            }
        }

        private void PatchPlayerInitialization(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchStfld<RoomPlayerSession>(nameof(RoomPlayerSession.Data))))
                throw new InvalidOperationException("Shared player data initializer was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_S, (byte)4);
            cursor.EmitDelegate<Action<RoomPlayerSession, ISourceEngineContext>>((player, owner) =>
            {
                if (ReferenceEquals(owner, engine)) player.Data.Stamina = runtime.Get<int>(Variant.Stamina);
            });
        }

        private bool InfiniteStamina =>
            ((IOriginalGameplayProcess)engine).SaveContext.CurrentSave.Assists.InfiniteStamina;

        private float StaminaAmount(float original, ISourceEngineContext owner)
        {
            if (!ReferenceEquals(owner, engine)) return original;
            int maximum = runtime.Get<int>(Variant.Stamina);
            if (runtime.Get<bool>(Variant.DontRefillStaminaOnGround) && !InfiniteStamina && !forceRefill)
            {
                float current = maximum;
                if (engine.Frame.Scenes.CurrentScene is ISourceScene scene)
                {
                    var players = scene.SourceScene.Entities.GetTrackedEntities(sourcePlayerRegistryKey);
                    if (players.Count > 0) current = ((RoomPlayerSession)players[0]).Data.Stamina;
                }

                if (current <= maximum) return current;
            }

            return maximum == 110 ? original : maximum;
        }

        private void RefillStamina(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, engine))
            {
                original(player);
                return;
            }

            if (runtime.Get<bool>(Variant.DontRefillStaminaOnGround) && !InfiniteStamina && !forceRefill) return;
            original(player);
            int maximum = runtime.Get<int>(Variant.Stamina);
            if (maximum != 110) player.Data.Stamina = maximum;
        }

        private void OnTransition(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, engine))
            {
                original(player);
                return;
            }

            forceRefill = true;
            try
            {
                original(player);
            }
            finally
            {
                forceRefill = false;
            }
        }

        private bool UseRefill(Func<RoomPlayerSession, bool, bool> original, RoomPlayerSession player, bool twoDashes)
        {
            if (!ReferenceEquals(player.EngineContext, engine)) return original(player, twoDashes);
            forceRefill = true;
            try
            {
                return original(player, twoDashes);
            }
            finally
            {
                forceRefill = false;
            }
        }

        private void ConstructPlayer(PlayerConstructor original, RoomPlayerSession player, Vector2 spawn,
            IPlayerAssistSettings settings, Celeste.Input.Gameplay.ISourceGameInput input, ISourceEngineContext owner,
            PlatformCollisionWorld world, IReadOnlyDictionary<string, string> sounds, PlayerSpriteMode mode)
        {
            if (!ReferenceEquals(owner, engine))
            {
                original(player, spawn, settings, input, owner, world, sounds, mode);
                return;
            }

            forceRefill = true;
            try
            {
                original(player, spawn, settings, input, owner, world, sounds, mode);
            }
            finally
            {
                forceRefill = false;
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            forceRefill = false;
        }
    }
}
