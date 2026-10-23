using System;
using Celeste.Everest.Modules.Capabilities;
using Celeste.Mods.ExtendedVariants.Configuration;
using Celeste.Mods.ExtendedVariants.Runtime;

namespace Celeste.Mods.ExtendedVariants.Modules
{
    public sealed partial class ExtendedVariantsModule : ISourceVariantValues
    {
        public Type VariantEnumType => typeof(Variant);
        public Type GetVariantValueType(object variant) => variants.GetDefaultValue((Variant)variant).GetType();
        public object GetCurrentVariantValue(object variant) => variants.GetCurrent((Variant)variant);
        public object GetCurrentMapDefinedVariantValue(object variant) => variants.GetMapDefined((Variant)variant);
        public void SetVariantValue(object variant, object value) => variants.SetUserValue((Variant)variant, value);
    }
}
