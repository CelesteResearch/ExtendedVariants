using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime.VanillaVariants;

namespace Celeste.Mods.ExtendedVariants.Runtime
{
    public static class VariantSettingsReset
    {
        public static void Vanilla(VariantRuntime runtime, Action resetIsaDefaults)
        {
            var assists = VanillaAssistState.For(runtime.Engine);
            assists.BeginPlayerChanges();
            foreach (Variant variant in VanillaAssistState.OrderedVariants)
            {
                runtime.Session.VariantsOverridenByUser.Remove(variant);
                assists.ChangedByPlayer(variant, VanillaAssistState.Current(runtime, variant, false));
                assists.ValueChanged(runtime, variant);
                runtime.NotifyResetValueChanged(variant);
            }

            resetIsaDefaults?.Invoke();
        }

        public static void Extended(VariantRuntime runtime)
        {
            foreach (Variant variant in runtime.GetSettings().EnabledVariants.Keys.ToArray())
            {
                runtime.GetSettings().EnabledVariants.Remove(variant);
                runtime.Session.VariantsOverridenByUser.Remove(variant);
                if (VariantRuntime.IsVanillaVariant(variant))
                    VanillaAssistState.For(runtime.Engine).ValueChanged(runtime, variant);
                runtime.NotifyResetValueChanged(variant);
            }
        }
    }
}
