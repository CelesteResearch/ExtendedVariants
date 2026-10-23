using System;
using System.Collections;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.BadelineBoosts;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Presentation.Hair;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.DashVariants
{
    public sealed partial class DashVariantProcess
    {
        private void InstallDashCount()
        {
            hooks.Add(new Hook(Method(typeof(RoomPlayerSession), "RefillDash"),
                (Func<Func<RoomPlayerSession, bool>, RoomPlayerSession, bool>)RefillDash));
            hooks.Add(new ILHook(Method(typeof(RoomPlayerSession), "UseRefill"), PatchUseRefill));
            hooks.Add(new ILHook(Method(typeof(PlayerHairAppearance), "UpdateColor"), PatchHairColor));
            hooks.Add(new Hook(Method(typeof(RoomPlayerSession), "Added"),
                (Action<Action<RoomPlayerSession, object>, RoomPlayerSession, object>)((orig, player, scene) =>
                {
                    orig(player, scene);
                    if (!ReferenceEquals(player.EngineContext, engine)) return;
                    int spawnCount = variants.Get<int>(Variant.SpawnDashCount);
                    player.Data.Dashes = spawnCount != -1 ? spawnCount : DetermineDashCount(player.Data.Dashes);
                })));
            hooks.Add(new Hook(Method(typeof(BadelineBoostState), "BoostRoutine"),
                (Func<Func<BadelineBoostState, RoomPlayerSession, IEnumerator>, BadelineBoostState, RoomPlayerSession,
                    IEnumerator>)
                ((orig, booster, player) => ReferenceEquals(player.EngineContext, engine)
                    ? AfterBadelineBoost(orig(booster, player), player)
                    : orig(booster, player))));
        }

        private int DetermineDashCount(int original)
        {
            int count = variants.Get<int>(Variant.DashCount);
            return count == -1 ? original : count;
        }

        private bool RefillDash(Func<RoomPlayerSession, bool> original, RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, engine)) return original(player);
            variants.NotifyDashRefilled();
            int count = variants.Get<int>(Variant.DashCount);
            if (count == -1) return original(player);
            if (player.Data.Dashes >= count) return false;
            player.Data.Dashes = count;
            return true;
        }

        private void PatchUseRefill(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchStloc(0)))
                throw new InvalidOperationException("Shared UseRefill has no original maximum dash local.");
            cursor.EmitLdarg0();
            cursor.EmitDelegate((Func<int, RoomPlayerSession, int>)((count, player) =>
                ReferenceEquals(player.EngineContext, engine) ? DetermineDashCount(count) : count));
            cursor.Index = 0;
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchStfld<PlayerState>(nameof(PlayerState.Dashes))))
                throw new InvalidOperationException("Shared UseRefill has no original dash refill assignment.");
            cursor.EmitLdarg0();
            cursor.EmitDelegate((Action<RoomPlayerSession>)(player =>
            {
                if (ReferenceEquals(player.EngineContext, engine)) variants.NotifyDashRefilled();
            }));
        }

        private void PatchHairColor(ILContext il)
        {
            var cursor = new ILCursor(il);
            int patched = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchLdcI4(2) &&
                                      (instruction.Next.OpCode == OpCodes.Bne_Un_S ||
                                       instruction.Next.OpCode == OpCodes.Bne_Un)))
            {
                cursor.EmitLdarg0();
                cursor.EmitDelegate((Func<int, PlayerHairAppearance, int>)((count, appearance) =>
                    hairOwners.TryGetValue(appearance, out _) && count >= 2 ? 2 : count));
                cursor.Index++;
                patched++;
            }

            if (patched != 2)
                throw new InvalidOperationException(
                    "Shared hair color has changed its two original dash-equality branches.");
        }

        private IEnumerator AfterBadelineBoost(IEnumerator original, RoomPlayerSession player)
        {
            yield return new SourceSwapImmediately(original);
            int count = variants.Get<int>(Variant.DashCount);
            if (count != -1) player.Data.Dashes = count < player.Inventory.Dashes ? count - 1 : count;
            variants.NotifyDashRefilled();
        }
    }
}
