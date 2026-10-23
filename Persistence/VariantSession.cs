using System.Collections.Generic;
using System.IO;
using Celeste.Everest.Modules.Persistence;
using Celeste.Mods.ExtendedVariants.Configuration;

namespace Celeste.Mods.ExtendedVariants.Persistence
{
    public sealed class VariantSession : EverestModuleBinarySession
    {
        public Dictionary<Variant, object> VariantsEnabledViaTrigger { get; set; } = new();
        public Dictionary<Variant, object> OverriddenVariantsInRoom { get; set; } = new();
        public Dictionary<Variant, List<object>> RevertOnLeaveVariantsActive { get; set; } = new();
        public HashSet<Variant> VariantsOverridenByUser { get; set; } = new();
        public bool ExtendedVariantsDisplayedOnScreenViaTrigger { get; set; }
        public bool ExtendedVariantsWereUsed { get; set; }
        public int DashCountOnLatestRespawn { get; set; } = -1;

        public override void Write(BinaryWriter writer)
        {
            writer.Write(1);
            writer.Write(VariantsEnabledViaTrigger.Count);
            foreach (KeyValuePair<Variant, object> pair in VariantsEnabledViaTrigger)
            {
                writer.Write((int)pair.Key);
                VariantValueCodec.Write(writer, pair.Value);
            }

            writer.Write(VariantsOverridenByUser.Count);
            foreach (Variant variant in VariantsOverridenByUser) writer.Write((int)variant);
            writer.Write(ExtendedVariantsWereUsed);
            writer.Write(DashCountOnLatestRespawn);
            writer.Write(ExtendedVariantsDisplayedOnScreenViaTrigger);
        }

        public override void Read(BinaryReader reader)
        {
            if (reader.ReadInt32() != 1)
                throw new InvalidDataException("Unsupported Extended Variants session version.");
            VariantsEnabledViaTrigger.Clear();
            int count = reader.ReadInt32();
            for (int index = 0; index < count; index++)
            {
                Variant variant = (Variant)reader.ReadInt32();
                VariantsEnabledViaTrigger.Add(variant, VariantValueCodec.Read(reader));
            }

            VariantsOverridenByUser.Clear();
            count = reader.ReadInt32();
            for (int index = 0; index < count; index++) VariantsOverridenByUser.Add((Variant)reader.ReadInt32());
            ExtendedVariantsWereUsed = reader.ReadBoolean();
            DashCountOnLatestRespawn = reader.ReadInt32();
            ExtendedVariantsDisplayedOnScreenViaTrigger = reader.ReadBoolean();
            OverriddenVariantsInRoom.Clear();
            RevertOnLeaveVariantsActive.Clear();
        }
    }
}
