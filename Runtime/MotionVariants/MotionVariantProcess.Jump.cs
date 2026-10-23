// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.MotionVariants
{
    internal sealed partial class MotionVariantProcess
    {
        private void InstallJumpHooks()
        {
            // Separate manipulators retain the upstream registration and float multiply order.
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.Jump),
                il => MultiplyPlayerConstant(il, -105f, Variant.JumpHeight));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperJump),
                il => MultiplyPlayerConstant(il, -105f, Variant.JumpHeight));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.WallJump),
                il => MultiplyPlayerConstant(il, -105f, Variant.JumpHeight));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperWallJump),
                il => MultiplyPlayerConstant(il, -160f, Variant.JumpHeight));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperJump),
                il => MultiplyPlayerConstant(il, 260f, Variant.SpeedX));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperWallJump),
                il => MultiplyPlayerConstant(il, 170f, Variant.SpeedX));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.WallJump),
                il => MultiplyPlayerConstant(il, 130f, Variant.SpeedX));
            using (new DetourConfigContext(new DetourConfig("ExtendedVariantMode_AfterAll").WithPriority(int.MaxValue))
                       .Use())
                hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.WallJump)),
                    (Action<Action<RoomPlayerSession, int>, RoomPlayerSession, int>)WallJump));
            AddIl(typeof(PlayerContacts), nameof(PlayerContacts.WallJumpCheck), PatchWallJumpCheck);
            hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.ClimbJump)),
                (Action<Action<RoomPlayerSession>, RoomPlayerSession>)ClimbJump));
            AddIl(typeof(ClimbSequence), nameof(ClimbSequence.Update), PatchClimbJumpInput);
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperJump),
                il => MultiplyPlayerConstant(il, 260f, Variant.HyperdashSpeed));
            hooks.Add(new Hook(PlayerMethod(nameof(RoomPlayerSession.ExplodeLaunch)),
                (Func<Func<RoomPlayerSession, Vector2, bool, bool, Vector2>, RoomPlayerSession, Vector2, bool, bool,
                    Vector2>)ExplodeLaunch));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.SuperWallJump),
                il => MultiplyPlayerConstant(il, -160f, Variant.WallBouncingSpeed));
            AddIl(typeof(RoomPlayerSession), nameof(RoomPlayerSession.WallJump), PatchNeutralJump);
        }

        private void WallJump(Action<RoomPlayerSession, int> original, RoomPlayerSession player, int direction)
        {
            if (Owns(player) && Flag(Variant.DisableWallJumping)) return;
            original(player, direction);
        }

        private void ClimbJump(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (Owns(player) && Flag(Variant.DisableClimbJumping)) return;
            original(player);
            if (Owns(player)) climbJumpGrabCooldown = .25f;
        }

        private void PatchWallJumpCheck(ILContext il)
        {
            var cursor = new ILCursor(il);
            var start = cursor.MarkLabel();
            cursor.MoveBeforeLabels();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld,
                typeof(PlayerContacts).GetField("actualOwner", BindingFlags.Instance | BindingFlags.NonPublic));
            cursor.EmitDelegate<Func<ISourceEntityCollider, bool>>(owner =>
                owner is RoomPlayerSession player && Owns(player) && Flag(Variant.DisableWallJumping));
            cursor.Emit(OpCodes.Brfalse, start);
            cursor.Emit(OpCodes.Ldc_I4_0);
            cursor.Emit(OpCodes.Ret);
        }

        private void PatchNeutralJump(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<PlayerState>(nameof(PlayerState.MoveX))))
                throw new InvalidOperationException("WallJump neutral input gate not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, RoomPlayerSession, int>>((value, player) =>
                Owns(player) && Flag(Variant.DisableNeutralJumping) ? 1 : value);
        }

        private void PatchClimbJumpInput(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instruction =>
                    instruction.OpCode.FlowControl == FlowControl.Call &&
                    instruction.Operand is MethodReference method && method.Name == "get_Pressed"))
                throw new InvalidOperationException("ClimbUpdate jump input gate not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld,
                typeof(ClimbSequence).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic));
            cursor.EmitDelegate<Func<bool, PlayerState, bool>>((pressed, state) =>
                !Active || !Flag(Variant.DisableClimbJumping) || state.MoveX == -state.Facing ? pressed : false);
        }

        private Vector2 ExplodeLaunch(Func<RoomPlayerSession, Vector2, bool, bool, Vector2> original,
            RoomPlayerSession player, Vector2 from, bool snapUp, bool sidesOnly)
        {
            Vector2 result = original(player, from, snapUp, sidesOnly);
            if (!Owns(player)) return result;
            // DisableSuperBoosts is implemented by the upstream ExplodeLaunchSpeed handler.
            player.Speed *= Factor(Variant.ExplodeLaunchSpeed);
            if (Flag(Variant.DisableSuperBoosts))
            {
                if (player.Input.MoveX.Value == Math.Sign(player.Speed.X)) player.Data.Motion.Normal.Speed.X /= 1.2f;
                else player.Data.ExplodeLaunchBoostTimer = 0f;
            }

            return result;
        }
    }
}
