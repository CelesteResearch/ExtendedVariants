using System;
using System.Reflection;
using Celeste.Core.Reflection.Interop;
using Celeste.Mods.ExtendedVariants.Runtime;
using MonoMod.ModInterop;

namespace Celeste.Mods.ExtendedVariants.Modules
{
    public sealed partial class ExtendedVariantsModule
    {
        private IDisposable resetInterop;

        [ModImportName("IsaGrabBag.ResetDefaultVariants")]
        public Action resetIsaDefaults;

        public void ResetToDefaultSettings()
        {
            ResetVanillaVariantsToDefaultSettings();
            ResetExtendedVariantsToDefaultSettings();
        }

        public void ResetVanillaVariantsToDefaultSettings() => VariantSettingsReset.Vanilla(variants, resetIsaDefaults);
        public void ResetExtendedVariantsToDefaultSettings() => VariantSettingsReset.Extended(variants);

        private void LoadResetInterop()
        {
            Type type = typeof(ExtendedVariantsModule);
            resetInterop = ((ISourceModInteropEnvironment)SourceEngine).ModInterop.RegisterOwned(
                new SourceModInteropActualTypeBinding(type, this, "ExtendedVariantMode",
                    exportedMethods: new[]
                    {
                        type.GetMethod(nameof(ResetToDefaultSettings)),
                        type.GetMethod(nameof(ResetVanillaVariantsToDefaultSettings)),
                        type.GetMethod(nameof(ResetExtendedVariantsToDefaultSettings))
                    },
                    importedFields: new[]
                        { type.GetField(nameof(resetIsaDefaults), BindingFlags.Instance | BindingFlags.Public) }));
        }

        private void UnloadResetInterop()
        {
            resetInterop?.Dispose();
            resetInterop = null;
            resetIsaDefaults = null;
        }
    }
}
