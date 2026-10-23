using Celeste.Core.Reflection.Runtime;
using System.Numerics;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Formats.Map.Entities.Structure;

namespace Celeste.Mods.ExtendedVariants.Triggers
{
    [CustomEntity("ExtendedVariantMode/ExtendedVariantController = Load")]
    [RuntimeTypeAlias("ExtendedVariants.Entities.Legacy.ExtendedVariantController")]
    public static class ExtendedVariantController
    {
        public static ExtendedVariantTrigger Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data)
        {
            return new ExtendedVariantTrigger(level.EngineContext, new EntityData
            {
                Position = new Vector2(0, -24f),
                Width = room.Bounds.Width,
                Height = room.Bounds.Height + 24,
                Values = data.Values
            }, offset);
        }
    }
}
