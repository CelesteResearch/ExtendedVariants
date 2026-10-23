using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.ExtendedVariants.Runtime.DashVariants
{
    public sealed partial class DashVariantProcess
    {
        private void InstallDashDirection()
        {
            hooks.Add(new Hook(typeof(DashSequence).GetProperty("CanDash").GetMethod,
                (Func<Func<DashSequence, bool>, DashSequence, bool>)CanDash));
            hooks.Add(new Hook(Method(typeof(DashSequence), "Start"),
                (Func<Func<DashSequence, int>, DashSequence, int>)((orig, self) =>
                {
                    if (dashOwners.TryGetValue(self, out var state)) Capture(state);
                    return orig(self);
                })));
            hooks.Add(new Hook(Method(typeof(PlayerBoostSequence), "BoostEnd"),
                (Action<Action<PlayerBoostSequence>, PlayerBoostSequence>)((orig, self) =>
                {
                    if (boostOwners.TryGetValue(self, out var state) &&
                        (state.Player.Machine.State == 2 || state.Player.Machine.State == 5)) Capture(state);
                    orig(self);
                })));
            hooks.Add(new Hook(Method(typeof(DashSequence), "Coroutine"),
                (Func<Func<DashSequence, IEnumerator>, DashSequence, IEnumerator>)((orig, self) =>
                    dashOwners.TryGetValue(self, out var state) && !AllDirectionsAllowed()
                        ? RestrictDash(orig(self), state)
                        : orig(self))));
            hooks.Add(new Hook(Method(typeof(PlayerBoostSequence), "RedDashCoroutine"),
                (Func<Func<PlayerBoostSequence, IEnumerator>, PlayerBoostSequence, IEnumerator>)((orig, self) =>
                    boostOwners.TryGetValue(self, out var state) && !AllDirectionsAllowed()
                        ? RestrictDash(orig(self), state)
                        : orig(self))));
        }

        private static void Capture(DashState state)
        {
            state.CountBeforeDash = state.Player.Data.Dashes;
            state.DirectionBeforeDash = state.Player.Data.LastAim;
        }

        private bool CanDash(Func<DashSequence, bool> original, DashSequence self)
        {
            if (!dashOwners.TryGetValue(self, out var state)) return original(self);
            Vector2 aim = self.Data.OverrideDirection ?? self.Data.LastAim;
            return original(self) && (self.Settings.DashAssist || DirectionAllowed(aim));
        }

        private bool AllDirectionsAllowed()
        {
            foreach (bool[] row in variants.Get<bool[][]>(Variant.DashDirection))
            foreach (bool allowed in row)
                if (!allowed)
                    return false;
            return true;
        }

        public bool DirectionAllowed(Vector2 direction) => variants.Get<bool[][]>(Variant.DashDirection)
            [Math.Sign(direction.Y) + 1][Math.Sign(direction.X) + 1];

        private IEnumerator RestrictDash(IEnumerator original, DashState state)
        {
            var routine = original.SafeEnumerate();
            if (routine.MoveNext()) yield return routine.Current;
            var player = state.Player;
            Vector2 direction = player.Data.OverrideDirection ?? player.Data.LastAim;
            if (DirectionAllowed(direction))
            {
                while (routine.MoveNext()) yield return routine.Current;
                yield break;
            }

            if (direction != state.DirectionBeforeDash && DirectionAllowed(state.DirectionBeforeDash))
            {
                player.Data.LastAim = state.DirectionBeforeDash;
                while (routine.MoveNext()) yield return routine.Current;
                yield break;
            }

            player.Data.CalledEvents = true;
            player.Speed = player.Data.BeforeDashSpeed;
            player.Data.Dashes = state.CountBeforeDash;
            player.Appearance.LastDashes = player.Data.Dashes;
            if (player.CurrentBooster != null)
            {
                player.CurrentBooster.PlayerReleased();
                player.CurrentBooster = null;
            }

            player.Machine.State = 0;
        }
    }
}
