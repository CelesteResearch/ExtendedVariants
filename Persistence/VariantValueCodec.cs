using System;
using System.IO;
using Celeste.Mods.ExtendedVariants.Configuration;

namespace Celeste.Mods.ExtendedVariants.Persistence
{
    public static class VariantValueCodec
    {
        public static void Write(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case int number:
                    writer.Write((byte)0);
                    writer.Write(number);
                    break;
                case float number:
                    writer.Write((byte)1);
                    writer.Write(number);
                    break;
                case bool boolean:
                    writer.Write((byte)2);
                    writer.Write(boolean);
                    break;
                case string text:
                    writer.Write((byte)3);
                    writer.Write(text);
                    break;
                case SpeedometerConfiguration speedometer:
                    writer.Write((byte)4);
                    writer.Write((int)speedometer);
                    break;
                case DashRefillOnGroundConfiguration refill:
                    writer.Write((byte)5);
                    writer.Write((int)refill);
                    break;
                case bool[][] directions:
                    writer.Write((byte)6);
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                        writer.Write(directions[row][column]);
                    break;
                default:
                    throw new NotSupportedException("Variant value type has no installed codec: " + value.GetType());
            }
        }

        public static object Read(BinaryReader reader)
        {
            switch (reader.ReadByte())
            {
                case 0: return reader.ReadInt32();
                case 1: return reader.ReadSingle();
                case 2: return reader.ReadBoolean();
                case 3: return reader.ReadString();
                case 4: return (SpeedometerConfiguration)reader.ReadInt32();
                case 5: return (DashRefillOnGroundConfiguration)reader.ReadInt32();
                case 6:
                    bool[][] directions = { new bool[3], new bool[3], new bool[3] };
                    for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                        directions[row][column] = reader.ReadBoolean();
                    return directions;
                default: throw new InvalidDataException("Unknown variant value encoding.");
            }
        }
    }
}
