using Celeste.Core.Reflection.Runtime;
using System.Numerics;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.ExtendedVariants.Components;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/FlagToggledExtendedVariantTrigger = Load")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.Legacy.FlagToggledExtendedVariantTrigger")]
    public static class FlagToggledExtendedVariantTrigger
    {
        public static ExtendedVariantTrigger Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
        {
            var trigger = new ExtendedVariantTrigger(level.EngineContext, data, offset);
            trigger.Add(new FlagToggleComponent(data.Attr("flag"), data.Bool("inverted")));
            return trigger;
        }
    }
}
