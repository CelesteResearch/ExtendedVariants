using System;
using System.Collections;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Enemies.Seekers;
using Celeste.Game.Gameplay.Hazards.Bumpers;
using Celeste.Game.Gameplay.Hazards.Puffer;
using Celeste.Game.Gameplay.Hazards.TempleEyes;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Inventory;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.DashVariants
{
    public sealed partial class DashVariantProcess
    {
        private void InstallGroundRefills()
        {
            foreach (string method in new[] { "Bounce", "SuperBounce", "SideBounce", "DreamDashEnd" })
                hooks.Add(new ILHook(Method(typeof(RoomPlayerSession), method), PatchPlayerNoRefills));
            hooks.Add(new ILHook(Method(typeof(PlayerFrameMotion), "Update"), PatchFrameNoRefills));
            hooks.Add(new Hook(Method(typeof(OriginalBumperState), "OnPlayer"),
                (Action<Action<OriginalBumperState, RoomPlayerSession>, OriginalBumperState, RoomPlayerSession>)
                ((orig, bumper, player) => SwapNoRefills(() => orig(bumper, player), bumper.Scene))));
            hooks.Add(new Hook(Method(typeof(OriginalTempleBigEyeballState), "OnPlayer"),
                (Action<Action<OriginalTempleBigEyeballState, RoomPlayerSession>, OriginalTempleBigEyeballState,
                    RoomPlayerSession>)
                ((orig, eye, player) => SwapNoRefills(() => orig(eye, player), eye.Scene))));
            hooks.Add(new Hook(Method(typeof(OriginalPufferState), "Explode"),
                (Action<Action<OriginalPufferState>, OriginalPufferState>)((orig, puffer) =>
                    SwapNoRefills(() => orig(puffer), puffer.Scene))));
            hooks.Add(new Hook(Method(typeof(OriginalSeekerState), "RegenerateCoroutine"),
                (Func<Func<OriginalSeekerState, IEnumerator>, OriginalSeekerState, IEnumerator>)
                ((orig, seeker) => seeker.Scene is OriginalLevelSceneRoot level &&
                                   ReferenceEquals(level.EngineContext, engine)
                    ? WrapSeeker(orig(seeker), level)
                    : orig(seeker))));
        }

        public bool GroundRefillsDisabled(bool original) =>
            variants.Get<DashRefillOnGroundConfiguration>(Variant.DontRefillDashOnGround) switch
            {
                DashRefillOnGroundConfiguration.ON => true,
                DashRefillOnGroundConfiguration.OFF => false,
                _ => original
            };

        private static bool IsNoRefillsRead(Instruction instruction) =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
            instruction.Operand is Mono.Cecil.MethodReference method && method.Name == "get_NoRefills" ||
            instruction.MatchLdfld<RoomPlayerInventory>(nameof(RoomPlayerInventory.NoRefills));

        private void PatchPlayerNoRefills(ILContext il)
        {
            var cursor = new ILCursor(il);
            int patched = 0;
            while (cursor.TryGotoNext(MoveType.After, IsNoRefillsRead))
            {
                cursor.EmitLdarg0();
                cursor.EmitDelegate((Func<bool, RoomPlayerSession, bool>)((original, player) =>
                    ReferenceEquals(player.EngineContext, engine) ? GroundRefillsDisabled(original) : original));
                patched++;
            }

            if (patched != 1)
                throw new InvalidOperationException("Shared player no-refills site count changed: " + il.Method.Name);
        }

        private void PatchFrameNoRefills(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, IsNoRefillsRead))
                throw new InvalidOperationException("Shared player bookkeeping has no original no-refills read.");
            cursor.EmitLdarg0();
            cursor.EmitDelegate((Func<bool, PlayerFrameMotion, bool>)((original, frame) =>
                frameOwners.TryGetValue(frame, out _) ? GroundRefillsDisabled(original) : original));
        }

        private void SwapNoRefills(Action original, object scene)
        {
            if (scene is not OriginalLevelSceneRoot level || !ReferenceEquals(level.EngineContext, engine))
            {
                original();
                return;
            }

            var inventory = level.Session.Inventory;
            bool oldValue = inventory.NoRefills;
            inventory.NoRefills = GroundRefillsDisabled(oldValue);
            level.Session.Inventory = inventory;
            original();
            inventory = level.Session.Inventory;
            inventory.NoRefills = oldValue;
            level.Session.Inventory = inventory;
        }

        private IEnumerator WrapSeeker(IEnumerator original, OriginalLevelSceneRoot level)
        {
            var routine = original.SafeEnumerate();
            bool oldValue = level.Session.Inventory.NoRefills;
            while (routine.MoveNext())
            {
                yield return routine.Current;
                if (routine.Current is float time && time == .15f)
                {
                    var inventory = level.Session.Inventory;
                    oldValue = inventory.NoRefills;
                    inventory.NoRefills = GroundRefillsDisabled(oldValue);
                    level.Session.Inventory = inventory;
                }
            }

            var restored = level.Session.Inventory;
            restored.NoRefills = oldValue;
            level.Session.Inventory = restored;
        }
    }
}
