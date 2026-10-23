// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.Presentation.Animation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.MotionVariants
{
    internal sealed partial class MotionVariantProcess
    {
        private void InstallNormalHooks()
        {
            AddIl(typeof(NormalMotion), nameof(NormalMotion.UpdateHorizontal), PatchHorizontal);
            AddIl(typeof(NormalMotion), nameof(NormalMotion.UpdateFallLimit), PatchFallLimit);
            using (new DetourConfigContext(new DetourConfig("ExtendedVariantMode_AfterAll").WithPriority(int.MaxValue))
                       .Use())
                AddIl(typeof(NormalMotion), nameof(NormalMotion.UpdateGravityAndJump), PatchGravity);
            AddIl(typeof(NormalSequence), nameof(NormalSequence.Begin), PatchNormalBegin);
            AddIl(typeof(NormalSequence), "UpdateCore", PatchZeroGravityGrab);
            AddIl(typeof(PlayerAnimation), nameof(PlayerAnimation.Update), PatchAnimation);
        }

        private void MultiplyActiveConstant(ILContext il, float literal, Variant variant, bool firstOnly = false)
        {
            var cursor = new ILCursor(il);
            int matches = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(literal)))
            {
                cursor.EmitDelegate<Func<float, float>>(value => Active ? value * Factor(variant) : value);
                matches++;
                if (firstOnly) break;
            }

            if (matches == 0)
                throw new InvalidOperationException($"Missing actual {il.Method.FullName} motion literal {literal}.");
        }

        private void PatchHorizontal(ILContext il)
        {
            MultiplyActiveConstant(il, 500f, Variant.Friction, true);
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(.65f)))
                throw new InvalidOperationException("Normal horizontal air acceleration not found.");
            cursor.EmitDelegate<Func<float, float>>(value => Active ? value * Factor(Variant.AirFriction) : value);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(1f)))
                throw new InvalidOperationException("Normal horizontal ground acceleration not found.");
            cursor.EmitDelegate<Func<float, float>>(value => Active ? value * Factor(Variant.Friction) : value);

            cursor.Index = 0;
            int maximum = -1;
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(90f)) ||
                !cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchStloc(out maximum)))
                throw new InvalidOperationException("Normal maximum run-speed local not found.");
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.MatchLdarg(0),
                    instruction => instruction.MatchLdflda<NormalMotionState>(nameof(NormalMotionState.Speed)),
                    instruction => instruction.MatchLdfld<Vector2>(nameof(Vector2.X)),
                    instruction => instruction.MatchCall(typeof(Math), nameof(Math.Abs))))
                throw new InvalidOperationException("Normal maximum run-speed comparison point not found.");
            cursor.MoveAfterLabels();
            cursor.EmitLdloc(maximum);
            cursor.EmitDelegate<Func<float, float>>(value => Active ? value * Factor(Variant.SpeedX) : value);
            cursor.EmitStloc(maximum);
        }

        private void PatchFallLimit(ILContext il)
        {
            MultiplyActiveConstant(il, 160f, Variant.FallSpeed);
            MultiplyActiveConstant(il, 240f, Variant.FallSpeed);
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<NormalMotionContext>(nameof(NormalMotionContext.MoveY))))
                throw new InvalidOperationException("Normal fast-fall input gate not found.");
            cursor.EmitDelegate<Func<int, int>>(value => Active && Factor(Variant.FallSpeed) == 0f ? 0 : value);
        }

        private void PatchNormalBegin(ILContext il)
        {
            var cursor = new ILCursor(il);
            var owner = typeof(NormalSequence).GetField("engine", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(typeof(NormalSequence).FullName, "engine");
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(160f)))
                throw new InvalidOperationException("Normal state initial maxFall not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, owner);
            cursor.EmitDelegate<Func<float, ISourceEngineContext, float>>((value, context) =>
                ReferenceEquals(context, engine) ? value * Factor(Variant.FallSpeed) : value);
        }

        private void PatchGravity(ILContext il)
        {
            MultiplyActiveConstant(il, 160f, Variant.FallSpeed);
            var cursor = new ILCursor(il);
            int target = -1;
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdfld<NormalMotionState>(nameof(NormalMotionState.MaxFall)),
                    instruction => instruction.MatchStloc(out target)))
                throw new InvalidOperationException("Normal gravity target local not found.");
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(900f)))
                throw new InvalidOperationException("Normal downward acceleration not found.");
            cursor.EmitLdloc(target);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(NormalMotionState).GetField(nameof(NormalMotionState.Speed)));
            cursor.Emit(OpCodes.Ldfld, typeof(Vector2).GetField(nameof(Vector2.Y)));
            cursor.EmitDelegate<Func<float, float, float, float>>((value, targetSpeed, speed) =>
            {
                if (!Active) return value;
                float factor = Factor(Variant.Gravity);
                return value * (factor < 0f && speed > targetSpeed ? Math.Abs(factor) : factor);
            });
        }

        private void PatchZeroGravityGrab(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchCallOrCallvirt<Celeste.Input.Gameplay.ISourceGameInput>("get_GrabCheck")) ||
                !cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdfld<Vector2>(nameof(Vector2.Y))))
                throw new InvalidOperationException("Normal upward grab speed gate not found.");
            cursor.EmitDelegate<Func<float, float>>(value =>
                Active && Factor(Variant.Gravity) == 0f && climbJumpGrabCooldown <= 0f ? 0f : value);
        }

        private void PatchAnimation(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(25f)))
                throw new InvalidOperationException("Player idle animation threshold not found.");
            cursor.EmitDelegate<Func<float, float>>(value =>
            {
                if (!Active || Factor(Variant.Friction) >= 1f) return value;
                return 25f + (90f * Factor(Variant.SpeedX) - 25f) * (1f - Factor(Variant.Friction));
            });
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(160f)))
                throw new InvalidOperationException("Player falling animation threshold not found.");
            cursor.EmitDelegate<Func<float, float>>(value =>
                Active ? value * Math.Min(Factor(Variant.FallSpeed), Factor(Variant.Gravity)) - .1f : value);
        }

        private void VariantChanged(Variant variant)
        {
            if (variant != Variant.FallSpeed || engine.Frame.Scenes.CurrentScene is not ISourceScene scene) return;
            if (scene.SourceScene.Entities.TryGetTrackedEntities(typeof(RoomPlayerSession), out var players) &&
                players.Count != 0)
            {
                var player = (RoomPlayerSession)players[0];
                player.Data.Motion.Normal.MaxFall =
                    Math.Min(player.Data.Motion.Normal.MaxFall, 240f * Factor(Variant.FallSpeed));
            }
        }
    }
}
