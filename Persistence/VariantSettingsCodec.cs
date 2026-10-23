using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Celeste.Mods.ExtendedVariants.Configuration;

namespace Celeste.Mods.ExtendedVariants.Persistence
{
    public static class VariantSettingsCodec
    {
        public static Dictionary<Variant, object> Encode(Dictionary<Variant, object> values)
        {
            var result = new Dictionary<Variant, object>();
            foreach (var entry in values)
            {
                result.Add(entry.Key, entry.Value switch
                {
                    string value => "string:" + value,
                    int value => "int:" + value.ToString(CultureInfo.InvariantCulture),
                    float value => "float:" + value.ToString("R", CultureInfo.InvariantCulture),
                    bool value => "bool:" + value,
                    bool[][] value => "DashDirection:" + string.Join(",", value.SelectMany(row => row)),
                    SpeedometerConfiguration value => "DisplaySpeedometer:" + value,
                    DashRefillOnGroundConfiguration value => "DontRefillDashOnGround:" + value,
                    _ => throw new NotSupportedException($"Unsupported setting value for {entry.Key}.")
                });
            }

            return result;
        }

        public static Dictionary<Variant, object> Decode(Dictionary<Variant, object> values)
        {
            var result = new Dictionary<Variant, object>();
            foreach (var entry in values)
            {
                // Original settings encode every value with a type prefix to survive YAML object coercion.
                if (entry.Value is not string text) throw new FormatException($"Missing setting type for {entry.Key}.");
                int separator = text.IndexOf(':');
                if (separator < 0) throw new FormatException($"Missing setting type for {entry.Key}.");
                string kind = text.Substring(0, separator);
                string value = text.Substring(separator + 1);
                result.Add(entry.Key, kind switch
                {
                    "string" => value,
                    "int" => int.Parse(value, CultureInfo.InvariantCulture),
                    "float" => ParseFloat(value),
                    "bool" => bool.Parse(value),
                    "DashDirection" => ParseDirection(value),
                    "DisplaySpeedometer" => (SpeedometerConfiguration)Enum.Parse(typeof(SpeedometerConfiguration),
                        value),
                    "DontRefillDashOnGround" => (DashRefillOnGroundConfiguration)Enum.Parse(
                        typeof(DashRefillOnGroundConfiguration), value),
                    _ => throw new NotSupportedException($"Unsupported setting type {kind}.")
                });
            }

            return result;
        }

        private static float ParseFloat(string value) => float.TryParse(value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : float.Parse(value, CultureInfo.CurrentCulture);

        private static bool[][] ParseDirection(string value)
        {
            string[] values = value.Split(',');
            if (values.Length != 9) throw new FormatException("Dash direction requires nine values.");
            return new[]
            {
                new[] { bool.Parse(values[0]), bool.Parse(values[1]), bool.Parse(values[2]) },
                new[] { bool.Parse(values[3]), bool.Parse(values[4]), bool.Parse(values[5]) },
                new[] { bool.Parse(values[6]), bool.Parse(values[7]), bool.Parse(values[8]) }
            };
        }
    }
}
