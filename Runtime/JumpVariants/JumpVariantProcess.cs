using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.JumpVariants
{
    public sealed class JumpVariantProcess : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, JumpVariantProcess> Instances = new();
        private readonly ISourceEngineContext engine;
        private readonly VariantRuntime variants;
        private readonly List<IDisposable> hooks = new();
        private int buffer;
        private float cooldown;
        public int Buffer => buffer;

        public static JumpVariantProcess For(ISourceEngineContext engine) =>
            Instances.GetValue(engine, owner => new JumpVariantProcess(owner));

        private JumpVariantProcess(ISourceEngineContext engine)
        {
            this.engine = engine;
            variants = VariantRuntime.For(engine);
        }

        public static IDisposable Install(ISourceEngineContext engine)
        {
            JumpVariantProcess process = For(engine);
            try
            {
                process.hooks.Add(new ILHook(
                    typeof(NormalSequence).GetMethod("UpdateCore", BindingFlags.NonPublic | BindingFlags.Instance),
                    process.PatchNormal));
                process.hooks.Add(new ILHook(typeof(DashSequence).GetMethod(nameof(DashSequence.Update)),
                    process.PatchDash));
                process.hooks.Add(new ILHook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.UseRefill)),
                    process.PatchRefill));
                process.hooks.Add(new ILHook(PlayerMethod("DreamDashUpdate"), process.PatchDreamJump));
                process.hooks.Add(new Hook(PlayerMethod("DreamDashEnd"),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)process.DreamDashEnd));
                process.hooks.Add(new Hook(
                    typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.ClimbHopBlockedCheck)),
                    (Func<Func<RoomPlayerWorld, bool>, RoomPlayerWorld, bool>)process.ClimbHopBlocked));
                process.variants.DashRefilled += process.DashRefilled;
                process.variants.Changed += process.VariantChanged;
                Celeste.Everest.Runtime.Events.Player.EverestPlayerSourceEvents.OnAfterUpdate += process.AfterUpdate;
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private static MethodInfo PlayerMethod(string name) =>
            typeof(RoomPlayerSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException(name);

        private void PatchNormal(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<NormalSequence>>(sequence =>
            {
                if (ReferenceEquals(sequence.EngineContext, engine)) RefillOnGround(sequence.Data);
            });
            int count = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<PlayerMotionState>(nameof(PlayerMotionState.JumpGraceTimer))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, NormalSequence, float>>((grace, sequence) =>
                {
                    if (!ReferenceEquals(sequence.EngineContext, engine)) return grace;
                    bool right = sequence.World.WallJumpCheck(1);
                    bool left = sequence.World.WallJumpCheck(-1);
                    return CanJump(grace, sequence.Data, sequence.World, right, left);
                });
                count++;
            }

            if (count == 0) throw new InvalidOperationException("Normal jump grace read was not found.");
        }

        private void PatchDash(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<DashSequence>>(sequence =>
            {
                if (ReferenceEquals(sequence.EngineContext, engine)) RefillOnGround(sequence.Data);
            });
            int count = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<PlayerMotionState>(nameof(PlayerMotionState.JumpGraceTimer))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, DashSequence, float>>((grace, sequence) =>
                {
                    if (!ReferenceEquals(sequence.EngineContext, engine)) return grace;
                    bool right = sequence.World.WallJumpCheck(1);
                    bool left = sequence.World.WallJumpCheck(-1);
                    return CanJump(grace, sequence.Data, sequence.World, right, left);
                });
                count++;
            }

            if (count == 0) throw new InvalidOperationException("Dash jump grace read was not found.");
        }

        private void RefillOnGround(PlayerState data)
        {
            if (data.Motion.JumpGraceTimer > 0f && variants.Get<bool>(Variant.ResetJumpCountOnGround))
                buffer = variants.Get<int>(Variant.JumpCount) - 1;
        }

        private float CanJump(float grace, PlayerState data, object contactWorld, bool right, bool left)
        {
            int maximum = variants.Get<int>(Variant.JumpCount);
            if (maximum == 0 && buffer <= 0) return 0f;
            bool canUnDuck = contactWorld is INormalWorld normal
                ? normal.CanUnDuck
                : ((IDashWorld)contactWorld).CanUnDuck;
            if (canUnDuck && (left || right ||
                              contactWorld is RoomPlayerWorld world && world.Contacts.WaterAt(Vector2.UnitY * 2f)))
                return grace;
            if (grace > 0f || maximum != int.MaxValue && buffer <= 0 || cooldown > 0f) return grace;
            buffer--;
            cooldown = variants.Get<float>(Variant.JumpCooldown);
            data.DreamJump = false;
            return 1f;
        }

        private void PatchDreamJump(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is MethodReference reference && reference.Name == "get_Jump",
                    instruction => instruction.Operand is MethodReference reference && reference.Name == "get_Pressed"))
                throw new InvalidOperationException("Dream dash jump press was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<bool, RoomPlayerSession, bool>>((pressed, player) =>
                !ReferenceEquals(player.EngineContext, engine) || variants.Get<int>(Variant.JumpCount) != 0
                    ? pressed
                    : false);
        }

        private void PatchRefill(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<PlayerState>(nameof(PlayerState.Dashes)),
                    instruction => instruction.MatchLdloc(0),
                    instruction => instruction.OpCode == OpCodes.Blt || instruction.OpCode == OpCodes.Blt_S))
                throw new InvalidOperationException("UseRefill dash comparison was not found.");
            object target = cursor.Prev.Operand;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<RoomPlayerSession, bool>>(player =>
                ReferenceEquals(player.EngineContext, engine) && NeedsRefill);
            cursor.Emit(OpCodes.Brtrue, target);
        }

        private bool NeedsRefill => variants.Get<bool>(Variant.RefillJumpsOnDashRefill) &&
                                    variants.Get<int>(Variant.JumpCount) >= 2 &&
                                    buffer < variants.Get<int>(Variant.JumpCount) - 1;

        private void DashRefilled()
        {
            if (variants.Get<bool>(Variant.RefillJumpsOnDashRefill) &&
                variants.Get<int>(Variant.JumpCount) >= 2) Refill();
        }

        private void DreamDashEnd(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (ReferenceEquals(player.EngineContext, engine) &&
                variants.Get<bool>(Variant.ResetJumpCountOnGround)) Refill();
        }

        private bool ClimbHopBlocked(Func<RoomPlayerWorld, bool> original, RoomPlayerWorld world)
        {
            if (original(world)) return true;
            return ReferenceEquals(world.EngineContext, engine) && variants.Get<bool>(Variant.ResetJumpCountOnGround) &&
                   buffer > variants.Get<int>(Variant.JumpCount) - 1;
        }

        private void VariantChanged(Variant variant)
        {
            if (variant == Variant.JumpCooldown)
                cooldown = Math.Min(cooldown, variants.Get<float>(Variant.JumpCooldown));
        }

        private void AfterUpdate(RoomPlayerSession player)
        {
            if (ReferenceEquals(player.EngineContext, engine) && cooldown > 0f) cooldown -= engine.DeltaTime;
        }

        public bool Refill()
        {
            int before = buffer;
            buffer = variants.Get<int>(Variant.JumpCount) - 1;
            return before != buffer;
        }

        public bool Add(int count, bool capped, int cap)
        {
            int before = buffer;
            buffer = Math.Max(0, buffer);
            buffer += count;
            if (capped) buffer = Math.Min(buffer, cap == -1 ? variants.Get<int>(Variant.JumpCount) - 1 : cap);
            return before != buffer;
        }

        public void Set(int count, bool cap) => buffer = cap ? Math.Min(buffer, count) : count;

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            variants.DashRefilled -= DashRefilled;
            variants.Changed -= VariantChanged;
            Celeste.Everest.Runtime.Events.Player.EverestPlayerSourceEvents.OnAfterUpdate -= AfterUpdate;
            Instances.Remove(engine);
        }
    }
}
