using System;
using System.Collections.Generic;
using Celeste.Everest.Modules.Settings;
using Celeste.Mods.ExtendedVariants.Configuration;

namespace Celeste.Mods.ExtendedVariants.Persistence
{
    public sealed class VariantSettings : EverestModuleSettings
    {
        private bool masterSwitch;
        public event Action<bool> MasterSwitchChanged;

        public bool MasterSwitch
        {
            get => masterSwitch;
            set
            {
                if (masterSwitch == value) return;
                masterSwitch = value;
                MasterSwitchChanged?.Invoke(value);
            }
        }

        public Dictionary<Variant, object> EnabledVariants { get; set; } = new();
        public bool DisplayEnabledVariantsToScreen { get; set; }
        public float OnScreenDisplayTextOpacity { get; set; } = 1f;
        public float OnScreenDisplayBackgroundOpacity { get; set; } = .8f;
    }
}
