using System.ComponentModel;
using System.Globalization;

namespace VIP_AntiFlash;

/// <summary>Supports the original numeric VIP group value without changing VIPCore.</summary>
[TypeConverter(typeof(AntiFlashSettingsConverter))]
public sealed class AntiFlashSettings
{
    public int Mode { get; set; }
}

public sealed class AntiFlashSettingsConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mode)
                && mode is >= 0 and <= 3)
                return new AntiFlashSettings { Mode = mode };
            throw new FormatException("vip.antiflash must be an integer from 0 to 3.");
        }
        return base.ConvertFrom(context, culture, value);
    }
}
