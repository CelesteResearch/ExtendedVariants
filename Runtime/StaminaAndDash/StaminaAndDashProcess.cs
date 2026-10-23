// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;

namespace Celeste.Mods.ExtendedVariants.Runtime.StaminaAndDash
{
    public sealed class StaminaAndDashProcess : IDisposable
    {
        private readonly List<IDisposable> hooks = new();

        private StaminaAndDashProcess(ISourceEngineContext engine)
        {
            try
            {
                hooks.Add(new StaminaVariantHooks(engine));
                hooks.Add(new DashVariantHooks(engine));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public static IDisposable Install(ISourceEngineContext engine) => new StaminaAndDashProcess(engine);

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
