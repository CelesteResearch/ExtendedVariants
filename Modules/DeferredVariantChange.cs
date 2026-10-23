using Celeste.Core.Reflection.Runtime;
using System;
using Celeste.Core.Framework.Components;

namespace Celeste.Mods.ExtendedVariants.Modules
{
    internal sealed class DeferredVariantChange : SourceGameComponentState
    {
        private readonly Action change;
        private readonly Action<DeferredVariantChange> completed;

        public DeferredVariantChange(SourceGameFrameworkState game, Action change,
            Action<DeferredVariantChange> completed) : base(game)
        {
            this.change = change;
            this.completed = completed;
        }

        public override void Update(SourceGameTime gameTime)
        {
            change();
            Game.Components.Remove(this);
            completed(this);
        }
    }
}
