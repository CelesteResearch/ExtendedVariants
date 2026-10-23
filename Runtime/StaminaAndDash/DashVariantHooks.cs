// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Audio.Frame;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.ExtendedVariants.Runtime.StaminaAndDash
{
    internal sealed class DashVariantHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime runtime;
        private readonly List<IDisposable> hooks = new();

        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal DashVariantHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            runtime = VariantRuntime.For(engine);
            try
            {
                hooks.Add(new Hook(typeof(DashSequence).GetMethod(nameof(DashSequence.Begin)),
                    (Action<Action<DashSequence>, DashSequence>)DashBegin));
                var coroutine = typeof(DashSequence).GetMethod(nameof(DashSequence.Coroutine));
                hooks.Add(new ILHook(coroutine.GetStateMachineTarget(), PatchDashCoroutine));
                hooks.Add(new ILHook(typeof(DashSequence).GetProperty(nameof(DashSequence.CanDash)).GetMethod,
                    PatchCanDashCooldown));
                hooks.Add(new ILHook(typeof(PlayerFrameMotion).GetMethod(nameof(PlayerFrameMotion.Update)),
                    PatchFrameCooldown));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void DashBegin(Action<DashSequence> original, DashSequence dash)
        {
            original(dash);
            if (!ReferenceEquals(dash.EngineContext, engine)) return;
            float length = runtime.Get<float>(Variant.DashLength);
            if (length != 1f)
            {
                bool super = dash.Settings.SuperDashing;
                dash.Data.Motion.DashAttackTimer = (super ? .3f : .15f) * length + .15f;
                dash.Data.Motion.GliderBoostTimer = (super ? .3f : .15f) * length + (super ? .25f : .4f);
            }

            float timerMultiplier = runtime.Get<float>(Variant.DashTimerMultiplier);
            if (timerMultiplier != 1f)
                dash.Data.Motion.DashAttackTimer = timerMultiplier * dash.Data.Motion.DashAttackTimer;
        }

        private void PatchDashCoroutine(ILContext il)
        {
            var coroutine = typeof(DashSequence).GetMethod(nameof(DashSequence.Coroutine));
            var stateType = coroutine.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            var owner = stateType.GetField("<>4__this", InstanceMembers);
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After,
                       instruction => instruction.MatchLdcR4(.3f) || instruction.MatchLdcR4(.15f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, owner);
                cursor.EmitDelegate<Func<float, DashSequence, float>>((original, dash) =>
                    ReferenceEquals(dash.EngineContext, engine)
                        ? original * runtime.Get<float>(Variant.DashLength)
                        : original);
            }

            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.AfterLabel,
                       instruction => instruction.MatchStfld<PlayerState>(nameof(PlayerState.TrailCounter))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, owner);
                cursor.EmitDelegate<Func<int, DashSequence, int>>(DashTrailCounter);
                cursor.Index++;
            }
        }

        private int DashTrailCounter(int original, DashSequence dash)
        {
            if (!ReferenceEquals(dash.EngineContext, engine)) return original;
            float length = runtime.Get<float>(Variant.DashLength);
            if (length == 1f) return original;
            float duration = dash.Settings.SuperDashing ? .3f : .15f;
            return (int)Math.Round(duration * length) - 1;
        }

        private void PatchCanDashCooldown(ILContext il)
        {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.After, IsCooldownRead))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Call,
                    typeof(DashSequence).GetProperty(nameof(DashSequence.EngineContext)).GetMethod);
                cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>(Cooldown);
            }
        }

        private void PatchFrameCooldown(ILContext il)
        {
            var cursor = new ILCursor(il);
            var world = typeof(PlayerFrameMotion).GetField("world", InstanceMembers);
            while (cursor.TryGotoNext(MoveType.After, IsCooldownRead))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, world);
                cursor.EmitDelegate<Func<float, IPlayerFrameWorld, float>>((original, frameWorld) =>
                    frameWorld is RoomPlayerWorld actual ? Cooldown(original, actual.EngineContext) : original);
            }
        }

        private static bool IsCooldownRead(Instruction instruction)
            => instruction.MatchLdfld<PlayerState>(nameof(PlayerState.CooldownTimer)) ||
               instruction.MatchLdfld<PlayerState>(nameof(PlayerState.RefillCooldownTimer));

        private float Cooldown(float original, ISourceEngineContext owner)
            => ReferenceEquals(owner, engine) && runtime.Get<bool>(Variant.DisableDashCooldown) ? 0f : original;

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
