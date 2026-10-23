using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Framework.Components;
using Celeste.Everest.Runtime.Startup;
using Celeste.Everest.Runtime.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Everest.Modules.Runtime;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;
using Celeste.Mods.ExtendedVariants.Persistence;
using Celeste.Mods.ExtendedVariants.Runtime;
using Celeste.Mods.ExtendedVariants.Runtime.JumpVariants;
using Celeste.Mods.ExtendedVariants.Runtime.DashVariants;
using Celeste.Mods.ExtendedVariants.Runtime.MotionVariants;
using Celeste.Mods.ExtendedVariants.Runtime.StaminaAndDash;
using Celeste.Mods.ExtendedVariants.Runtime.VanillaVariants;
using Celeste.Mods.ExtendedVariants.Runtime.VisualVariants;
using Celeste.Mods.ExtendedVariants.Runtime.Water;
using Celeste.Mods.ExtendedVariants.Runtime.TimingVariants;
using Celeste.Mods.ExtendedVariants.Runtime.ColorGrading;
using Celeste.Mods.ExtendedVariants.Runtime.VariantsDisplay;
using Celeste.Mods.ExtendedVariants.Runtime.VisualHud;
using Celeste.Mods.ExtendedVariants.Runtime.VisualHud.Indicators;

namespace Celeste.Mods.ExtendedVariants.Modules
{
    [RuntimeTypeAlias("ExtendedVariants.Module.ExtendedVariantsModule")]
    public sealed partial class ExtendedVariantsModule : SourceEverestModule
    {
        private readonly List<IDisposable> processes = new();
        private readonly List<DeferredVariantChange> pendingChanges = new();
        private VariantRuntime variants;
        private bool handlersInstalled;
        public override Type SessionType => typeof(VariantSession);
        public override Type SettingsType => typeof(VariantSettings);
        public VariantSession Session => (VariantSession)_Session;
        public VariantSettings Settings => (VariantSettings)_Settings;

        public ExtendedVariantsModule(ISourceEngineContext engine) : base(engine)
        {
        }

        public override void Load()
        {
            variants = VariantRuntime.For(SourceEngine);
            variants.Bind(() => Session, () => Settings);
            LoadResetInterop();
            try
            {
                processes.Add(new VariantTriggerLifecycle(SourceEngine));
                Settings.MasterSwitchChanged += SetEnabled;
                if (Settings.MasterSwitch) Activate();
            }
            catch
            {
                Unload();
                throw;
            }
        }

        private void SetEnabled(bool enabled)
        {
            if (!((IEverestGameInitializationEnvironment)SourceEngine).InitializationRuntime.IsInitialized)
            {
                SetEnabledNow(enabled);
                return;
            }

            SourceGameFrameworkState game = ((ISourceEverestMainThreadEnvironment)SourceEngine).MainThreadProcess.Game;
            var change = new DeferredVariantChange(game, () => SetEnabledNow(enabled),
                completed => pendingChanges.Remove(completed));
            pendingChanges.Add(change);
            game.Components.Add(change);
        }

        private void SetEnabledNow(bool enabled)
        {
            if (enabled) ActivateNow();
            else
            {
                for (int index = processes.Count - 1; index >= 1; index--)
                {
                    processes[index].Dispose();
                    processes.RemoveAt(index);
                }

                handlersInstalled = false;
            }
        }

        private void Activate() => SetEnabled(true);

        private void ActivateNow()
        {
            if (handlersInstalled) return;
            try
            {
                processes.Add(ColorGradingProcess.Install(SourceEngine));
                processes.Add(MotionVariantProcess.Install(SourceEngine));
                processes.Add(JumpVariantProcess.Install(SourceEngine));
                processes.Add(JumpIndicatorProcess.Install(SourceEngine));
                processes.Add(StaminaAndDashProcess.Install(SourceEngine));
                processes.Add(BounceVariantProcess.Install(SourceEngine));
                processes.Add(VanillaVariantProcess.Install(SourceEngine));
                processes.Add(VisualVariantProcess.Install(SourceEngine));
                processes.Add(UnderwaterVariantProcess.Install(SourceEngine));
                processes.Add(DashVariantProcess.Install(SourceEngine));
                processes.Add(TimingVariantProcess.Install(SourceEngine));
                processes.Add(VisualHudProcess.Install(SourceEngine));
                processes.Add(VariantsDisplayProcess.Install(SourceEngine));
                handlersInstalled = true;
            }
            catch
            {
                Unload();
                throw;
            }
        }

        public override void LoadSettings()
        {
            base.LoadSettings();
            Settings.EnabledVariants = VariantSettingsCodec.Decode(Settings.EnabledVariants);
        }

        public override void SaveSettings()
        {
            var actual = Settings.EnabledVariants;
            try
            {
                Settings.EnabledVariants = VariantSettingsCodec.Encode(actual);
                base.SaveSettings();
            }
            finally
            {
                Settings.EnabledVariants = actual;
            }
        }

        public override void LoadContent(bool firstLoad)
        {
            var registry = ((IEverestContentRegistryEnvironment)SourceEngine).SourceContentRegistry;
            var xml = new XmlDocument();
            using (var stream = registry.Get(Metadata.Name + ":/Graphics/Sprites").Stream) xml.Load(stream);
            variants.Sprites = new SpriteBank(SourceEngine,
                ((ISourceGameGraphicsEnvironment)SourceEngine).GameGraphics.GameAtlas, xml);
            DashCountIndicator.Initialize(SourceEngine);
            JumpIndicator.Initialize(SourceEngine);
        }

        public override void Unload()
        {
            UnloadResetInterop();
            foreach (DeferredVariantChange change in pendingChanges)
            {
                change.Enabled = false;
                change.Game.Components.Remove(change);
            }

            pendingChanges.Clear();
            for (int index = processes.Count - 1; index >= 0; index--) processes[index].Dispose();
            processes.Clear();
            if (variants != null)
            {
                Settings.MasterSwitchChanged -= SetEnabled;
            }

            handlersInstalled = false;
            variants?.Unload();
            variants = null;
        }
    }
}
