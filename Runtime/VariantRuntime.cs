using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Persistence;

namespace Celeste.Mods.ExtendedVariants.Runtime
{
    public sealed class VariantRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, VariantRuntime> Instances = new();
        private VariantSession fallbackSession = new();
        private readonly VariantSettings fallbackSettings = new();
        private Func<VariantSession> getSession;
        private Func<VariantSettings> getSettings;
        private readonly Dictionary<Variant, object> defaults = new();
        public VariantSession Session => getSession?.Invoke() ?? fallbackSession;
        public VariantSettings GetSettings() => getSettings?.Invoke() ?? fallbackSettings;
        public bool MasterSwitch => GetSettings().MasterSwitch;
        public object GetDefaultValue(Variant variant) => GetDefault(variant);
        public static bool IsVanillaVariant(Variant variant) => IsVanilla(variant);

        public IEnumerable<Variant> EnumerateActiveVariants()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Variant variant in VariantDefaults.SupportedVariants)
                {
                    if (IsVanilla(variant) != (pass == 0)) continue;
                    if (!Identical(GetCurrent(variant), GetDefault(variant))) yield return variant;
                }
            }
        }

        private Dictionary<Variant, object> session => Session.VariantsEnabledViaTrigger;
        private Dictionary<Variant, object> room => Session.OverriddenVariantsInRoom;
        private Dictionary<Variant, List<object>> leave => Session.RevertOnLeaveVariantsActive;
        private Action teleport;
        public event Action<Variant> Changed;
        internal void NotifyResetValueChanged(Variant variant) => Changed?.Invoke(variant);
        public event Action DashRefilled;
        public event Action ActivationRequested;

        public void ForceEnableForMap()
        {
            GetSettings().MasterSwitch = true;
            ActivationRequested?.Invoke();
        }

        public void NotifyDashRefilled() => DashRefilled?.Invoke();
        public List<string> ColorGrades { get; } = new();
        public Celeste.Formats.Graphics.Sprites.SpriteBank Sprites { get; set; }

        public static VariantRuntime For(ISourceEngineContext engine) =>
            Instances.GetValue(engine, owner => new VariantRuntime { Engine = owner });

        public T Get<T>(Variant variant) => (T)GetCurrent(variant);

        public object GetCurrent(Variant variant)
        {
            if (!Session.VariantsOverridenByUser.Contains(variant) &&
                Engine.Frame.Scenes.CurrentScene is not Celeste.Game.Menus.Overworld.OverworldSequence)
            {
                if (leave.TryGetValue(variant, out List<object> values)) return values[values.Count - 1];
                if (room.TryGetValue(variant, out object roomValue)) return roomValue;
                if (session.TryGetValue(variant, out object sessionValue)) return sessionValue;
            }

            VariantSettings settings = getSettings?.Invoke();
            return settings != null && settings.EnabledVariants.TryGetValue(variant, out object settingsValue)
                ? settingsValue
                : GetDefault(variant);
        }

        public void Bind(Func<VariantSession> sessionProvider, Func<VariantSettings> settingsProvider)
        {
            getSession = sessionProvider;
            getSettings = settingsProvider;
        }

        public object GetMapDefined(Variant variant)
        {
            if (Engine.Frame.Scenes.CurrentScene is not Celeste.Game.Menus.Overworld.OverworldSequence)
            {
                if (leave.TryGetValue(variant, out List<object> values)) return values[values.Count - 1];
                if (room.TryGetValue(variant, out object roomValue)) return roomValue;
                if (session.TryGetValue(variant, out object sessionValue)) return sessionValue;
            }

            return GetDefault(variant);
        }

        private object GetDefault(Variant variant)
        {
            if (defaults.TryGetValue(variant, out object value)) return value;
            defaults.Add(variant, value = VariantDefaults.Get(variant));
            return value;
        }

        public void SetUserValue(Variant variant, object value)
        {
            bool mapValueRestored = false;
            if (Engine.Frame.Scenes.CurrentScene is Celeste.Game.Execution.Levels.OriginalLevelSceneRoot)
            {
                if (Identical(value, GetMapDefined(variant)))
                {
                    Session.VariantsOverridenByUser.Remove(variant);
                    GetSettings().EnabledVariants.Remove(variant);
                    mapValueRestored = true;
                }
                else Session.VariantsOverridenByUser.Add(variant);
            }

            if (!mapValueRestored)
            {
                if (Identical(value, GetDefault(variant))) GetSettings().EnabledVariants.Remove(variant);
                else GetSettings().EnabledVariants[variant] = value;
            }

            Changed?.Invoke(variant);
        }

        public object ConvertLegacy(Variant variant, int value) =>
            VariantDefaults.ConvertLegacy(variant, value, ColorGrades);

        public void Enter(Variant variant, object value, bool revertOnLeave, bool revertOnDeath, bool legacy)
        {
            _ = GetDefault(variant);
            if (legacy) value = ConvertLegacy(variant, (int)value);
            if (revertOnLeave)
            {
                if (!leave.TryGetValue(variant, out List<object> values)) leave[variant] = values = new List<object>();
                values.Add(value);
            }
            else if (revertOnDeath) room[variant] = value;
            else
            {
                room.Remove(variant);
                leave.Remove(variant);
                SetSession(variant, value);
                return;
            }

            Changed?.Invoke(variant);
        }

        public void Leave(Variant variant, object value, bool legacy)
        {
            if (legacy) value = ConvertLegacy(variant, (int)value);
            if (!leave.TryGetValue(variant, out List<object> values)) return;
            // The source removes the FIRST equal value, even when duplicate triggers overlap.
            int index = values.FindIndex(candidate => Identical(candidate, value));
            if (index < 0) return;
            values.RemoveAt(index);
            if (values.Count == 0) leave.Remove(variant);
            Changed?.Invoke(variant);
        }

        public void CommitRoom()
        {
            if (room.Count == 0) return;
            foreach (KeyValuePair<Variant, object> pair in room) SetSession(pair.Key, pair.Value);
            ResetRoom();
        }

        public void ResetRoom()
        {
            foreach (Variant variant in room.Keys.ToArray())
            {
                room.Remove(variant);
                Changed?.Invoke(variant);
            }

            foreach (Variant variant in leave.Keys.ToArray())
            {
                leave.Remove(variant);
                Changed?.Invoke(variant);
            }
        }

        public void Reset(bool vanilla)
        {
            ResetValues(session, vanilla);
            ResetValues(room, vanilla);
            foreach (Variant variant in leave.Keys.ToArray())
            {
                if (IsVanilla(variant) != vanilla) continue;
                leave.Remove(variant);
                Changed?.Invoke(variant);
            }
        }

        public void QueueTeleport(Action apply) => teleport += apply;

        public void ApplyTeleport()
        {
            teleport?.Invoke();
            teleport = null;
        }

        public void Unload()
        {
            teleport = null;
            Instances.Remove(Engine);
            Changed = null;
            DashRefilled = null;
            ActivationRequested = null;
        }

        public ISourceEngineContext Engine { get; internal set; }

        private void SetSession(Variant variant, object value)
        {
            if (Identical(value, GetDefault(variant))) session.Remove(variant);
            else session[variant] = value;
            Changed?.Invoke(variant);
        }

        private void ResetValues(Dictionary<Variant, object> values, bool vanilla)
        {
            foreach (Variant variant in values.Keys.ToArray())
            {
                if (IsVanilla(variant) != vanilla) continue;
                values.Remove(variant);
                Changed?.Invoke(variant);
            }
        }

        private static bool IsVanilla(Variant variant) =>
            variant >= Variant.AirDashes && variant <= Variant.ThreeSixtyDashing;

        public static bool Identical(object one, object two)
        {
            if (one is bool[][] first && two is bool[][] second)
            {
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    if (!(x == 1 && y == 1) && first[y][x] != second[y][x])
                        return false;
                return true;
            }

            return one.Equals(two);
        }
    }
}
