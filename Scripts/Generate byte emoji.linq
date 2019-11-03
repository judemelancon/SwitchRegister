<Query Kind="Program">
  <NuGetReference>Magick.NET-Q8-AnyCPU</NuGetReference>
  <Namespace>ImageMagick</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
</Query>

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Program mode.
// It uses the NuGet feature, which requires a Developer or Premium license.
// Alternatively, it could be translated to a console program easily enough.

// Configuration
private static readonly string DestinationDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Emoji", "Generated", "Bytes");
private const int EmojiSize = 128;
private const string Font = "Inconsolata";
// End of Configuration

// This is in inches and is used to set the pixel density, which determines the font size and metrics.
private const double EmojiDisplaySize = 0.5;

private const decimal BackgroundMargin = 1.0m * EmojiSize / 128.0m;
private const decimal BitMarginX = 4.0m * EmojiSize / 128.0m;
private const decimal BitMarginY = 4.0m * EmojiSize / 128.0m;
private const decimal BitWidth = 15.0m * EmojiSize / 128.0m;
private const decimal BitHeight = 15.0m * EmojiSize / 128.0m;
private const decimal BitOffsetX = 15.0m * EmojiSize / 128.0m;
private const decimal BitOffsetY = 15.0m * EmojiSize / 128.0m;
private const double RoundedCornerSize = (double)(3.0m * EmojiSize / 128.0m);
private const double CharacterFontSize = 72.0;
private const double ControlCharacterFontSize = 42.0;
private const double CharacterX = (double)(4.0m * EmojiSize / 128.0m);
private const double CharacterY = (double)(124.0m * EmojiSize / 128.0m);
private const int CharacterSize = (int)(48.0m * EmojiSize / 128.0m);
private const Gravity CharacterDirection = Gravity.Northeast;
private static readonly MagickGeometry CharacterArea = new MagickGeometry((int)CharacterX, ((int)CharacterY) - CharacterSize, CharacterSize, CharacterSize);
private const double ValueFontSize = 48.0;
private const double ValueX = (double)(126.0m * EmojiSize / 128.0m);
private const double ValueY = (double)(-7.0m * EmojiSize / 128.0m);
private const Gravity ValueDirection = Gravity.Southwest;

private const byte BackgroundOpacity = 255;
private static readonly MagickColor ControlBackground = new MagickColor(117, 117, 117, BackgroundOpacity);
private static readonly MagickColor UppercaseBackground = new MagickColor(0, 132, 132, BackgroundOpacity);
private static readonly MagickColor LowercaseBackground = new MagickColor(60, 108, 240, BackgroundOpacity);
private static readonly MagickColor DigitBackground = new MagickColor(0, 136, 0, BackgroundOpacity);
private static readonly MagickColor PunctuationBackground = new MagickColor(128, 120, 60, BackgroundOpacity);
private static readonly MagickColor LeadingBackground = new MagickColor(208, 0, 208, BackgroundOpacity);
private static readonly MagickColor ContinuationBackground = new MagickColor(196, 84, 0, BackgroundOpacity);
private static readonly MagickColor InvalidBackground = new MagickColor(236, 0, 0, BackgroundOpacity);

private static readonly MagickColor CharacterColor = MagickColors.Black;
private static readonly MagickColor SpaceColor = new MagickColor(0, 0, 0, 128);
private static readonly MagickColor XonColor = new MagickColor(0, 208, 0);
private static readonly MagickColor XoffColor = new MagickColor(128, 0, 0);
private static readonly MagickColor ValueColor = MagickColors.Black;

void Main() {
    if (string.IsNullOrWhiteSpace(DestinationDirectory))
        throw new Exception($"{nameof(DestinationDirectory)} must be set");

    MagickNET.Version.Dump();

    Directory.CreateDirectory(DestinationDirectory);

    IDictionary<string, string> aliases = new Dictionary<string, string>();
    for (int current = byte.MinValue; current <= byte.MaxValue; ++current)
        aliases.AddRange(SaveEmoji((byte)current));
    for (int rowStart = byte.MinValue; rowStart < byte.MaxValue; rowStart += 8)
        Util.HorizontalRun(true,
                           Enumerable.Range(rowStart, 8)
                                     .Select(i => Path.Combine(DestinationDirectory, $"0x{i:x2}.png"))
                                     .Select(Util.Image))
            .Dump();
    File.WriteAllLines(Path.Combine(DestinationDirectory, "Aliases.txt"), aliases.Select(kvp => kvp.Key + " " + kvp.Value));
}

public static IEnumerable<KeyValuePair<string, string>> SaveEmoji(byte value) {
    using (IMagickImage emoji = DrawEmoji(value)) {
        emoji.Write(Path.Combine(DestinationDirectory, $"0x{value:x2}.png"));
    }
    return ListAliases(value);
}

public static IEnumerable<KeyValuePair<string, string>> ListAliases(byte value) {
    string canonicalName = $"0x{value:x2}";
    yield return new KeyValuePair<string, string>($"byte_{value}", canonicalName);
    //char character = (char)value;
    //if (char.IsLower(character) || char.IsDigit(character))
    //    yield return new KeyValuePair<string, string>($"unicode_{character}", canonicalName);
}

public static IMagickImage DrawEmoji(byte value) {
    IMagickImage emoji = new MagickImage(MagickColors.Transparent, EmojiSize, EmojiSize);
    try {
        emoji.Density = new Density(EmojiSize / EmojiDisplaySize, DensityUnit.PixelsPerInch);

        Drawables drawn = new Drawables();
        drawn.StrokeColor(MagickColors.Transparent)
             .FillColor(MagickColors.Black)
             .Font(Font, FontStyleType.Bold, FontWeight.Bold, FontStretch.Normal)
             .TextAntialias(true);

        {
            double start = (double)BackgroundMargin;
            double end = (double)(EmojiSize - BackgroundMargin - 1m);
            drawn.FillColor(GetBackground(value))
                 .RoundRectangle(start, start, end, end, RoundedCornerSize, RoundedCornerSize);
        }

        for (int i = 0; i < 8; ++i) {
            bool on = (value & (1 << i)) != 0;
            double x0 = (double)(BitMarginX + (7 - i) * BitOffsetX);
            double y0 = (double)(BitMarginY + (7 - i) * BitOffsetY);
            double x1 = (double)(BitMarginX + (7 - i) * BitOffsetX + BitWidth - 1m);
            double y1 = (double)(BitMarginY + (7 - i) * BitOffsetY + BitHeight - 1m);
            drawn.FillColor(on ? MagickColors.White : MagickColors.Black)
                 .RoundRectangle(x0, y0, x1, y1, RoundedCornerSize, RoundedCornerSize);
        }

        drawn.FillColor(ValueColor)
             .FontPointSize(ValueFontSize)
             .TextWithGravity(ValueX, ValueY, ValueDirection, value.ToString());

        using IDisposable _ = DrawRepresentation(drawn, value);

        drawn.Draw(emoji);

        return emoji;
    }
    catch {
        emoji.Dispose();
        throw;
    }
}

public static MagickColor GetBackground(byte value) {
    char character = (char)value;
    if (value < 0x80) {
        switch (char.GetUnicodeCategory(character)) {
            case UnicodeCategory.Control:
                return ControlBackground;
            case UnicodeCategory.LowercaseLetter:
                return LowercaseBackground;
            case UnicodeCategory.UppercaseLetter:
                return UppercaseBackground;
            case UnicodeCategory.DecimalDigitNumber:
                return DigitBackground;
            case UnicodeCategory.SpaceSeparator:
            case UnicodeCategory.OtherPunctuation:
            case UnicodeCategory.CurrencySymbol:
            case UnicodeCategory.OpenPunctuation:
            case UnicodeCategory.ClosePunctuation:
            case UnicodeCategory.MathSymbol:
            case UnicodeCategory.DashPunctuation:
            case UnicodeCategory.ModifierSymbol:
            case UnicodeCategory.ConnectorPunctuation:
                return PunctuationBackground;
            default:
                throw new Exception($"{character} is {char.GetUnicodeCategory(character)}");
        }
    }
    if (value < 0xC0)
        return ContinuationBackground;
    if (value >= 0xC2 && value <= 0xF4)
        return LeadingBackground;
    return InvalidBackground;
}

public static IDisposable DrawRepresentation(Drawables drawn, byte value) {
    if (value >= 128)
        return null;
    char character = (char)value;
    switch (char.GetUnicodeCategory(character)) {
        case UnicodeCategory.SpaceSeparator:
        case UnicodeCategory.Control:
            switch (value) {
                case 0x07: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Generalities/bronze_bell.png")));
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x08: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/backspace.png")));
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x09: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/tab.png")));
                        symbol.Modulate((Percentage)0);
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x0b: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/vertical_tab.png")));
                        symbol.Modulate((Percentage)0);
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x11:
                    drawn.FillColor(XonColor)
                         .FontPointSize(ControlCharacterFontSize);
                    break;
                case 0x13:
                    drawn.FillColor(XoffColor)
                         .FontPointSize(ControlCharacterFontSize);
                    break;
                case 0x18: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/cancel.png")));
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x1b: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/escape.png")));
                        symbol.Modulate((Percentage)0);
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                case 0x20:
                    drawn.FillColor(SpaceColor)
                         .FontPointSize(CharacterFontSize);
                    break;
                case 0x7F: {
                        IMagickImage symbol = new MagickImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji/Computing Symbols/delete.png")));
                        drawn.Composite(CharacterArea, CompositeOperator.Over, symbol);
                        return symbol;
                    }
                default:
                    drawn.FillColor(CharacterColor)
                         .FontPointSize(ControlCharacterFontSize);
                    break;
            }
            drawn.TextWithGravity(CharacterX, CharacterY, CharacterDirection, GetControlCharacterRepresentation(value));
            return null;
        case UnicodeCategory.LowercaseLetter:
        case UnicodeCategory.UppercaseLetter:
        case UnicodeCategory.DecimalDigitNumber:
        case UnicodeCategory.OtherPunctuation:
        case UnicodeCategory.CurrencySymbol:
        case UnicodeCategory.OpenPunctuation:
        case UnicodeCategory.ClosePunctuation:
        case UnicodeCategory.MathSymbol:
        case UnicodeCategory.DashPunctuation:
        case UnicodeCategory.ModifierSymbol:
        case UnicodeCategory.ConnectorPunctuation:
            drawn.FillColor(CharacterColor)
                 .FontPointSize(CharacterFontSize)
                 .TextWithGravity(CharacterX, CharacterY, CharacterDirection, character.ToString());
            return null;
        default:
            throw new Exception($"{character} is {char.GetUnicodeCategory(character)}");
    }
}

public static string GetControlCharacterRepresentation(byte value) {
    switch (value) {
        case 0x00:
            return "NUL";
        case 0x01:
            return "SOH";
        case 0x02:
            return "STX";
        case 0x03:
            return "ETX";
        case 0x04:
            return "EOT";
        case 0x05:
            return "Enq";
        case 0x06:
            return "Ack";
        //case 0x07:
        //    return "Bell";
        //case 0x08:
        //    return "⌫";
        //case 0x09:
        //    return "HT⭾";
        case 0x0a:
            // ↴
            return "LF";
        //case 0x0b:
        //    return "VT⭿";
        case 0x0c:
            return "FF";
        case 0x0d:
            // ↵
            return "CR";
        case 0x0e:
            return "SO";
        case 0x0f:
            return "SI";
        case 0x10:
            return "DLE";
        case 0x11:
            return "DC1";
        case 0x12:
            return "DC2";
        case 0x13:
            return "DC3";
        case 0x14:
            return "DC4";
        case 0x15:
            return "NAk";
        case 0x16:
            return "Syn";
        case 0x17:
            return "ETB";
        //case 0x18:
        //    return "Can";
        case 0x19:
            return "EM";
        case 0x1a:
            return "Sub";
        //case 0x1b:
        //    // ⮹ ⎋
        //    return "Esc";
        case 0x1c:
            return "FS";
        case 0x1d:
            return "GS";
        case 0x1e:
            return "RS";
        case 0x1f:
            return "US";
        case 0x20:
            return "␣";
        //case 0x7F:
        //    return "⌦";
        default:
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}

public static class DrawablesExtensions {
    public static Drawables TextWithGravity(this Drawables self, double x, double y, Gravity gravity, string value) {
        TypeMetric metrics = self.FontTypeMetrics(value);
        switch (gravity) {
            case Gravity.Center:
            case Gravity.North:
            case Gravity.South:
            case Gravity.West:
            case Gravity.East:
            case Gravity.Northwest:
            case Gravity.Southeast:
                throw new NotImplementedException("TODO");
            case Gravity.Northeast:
                x += 0.5 * metrics.TextWidth;
                y += metrics.Descent;
                break;
            case Gravity.Southwest:
                x -= 0.5 * metrics.TextWidth;
                y += metrics.Ascent;
                break;
            case Gravity.Undefined:
            default:
                throw new ArgumentOutOfRangeException(nameof(gravity));
        }
        return self.TextAlignment(TextAlignment.Center)
                   .Text(x, y, value);
    }
}

public static class DictionaryExtensions {
    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> self, IEnumerable<KeyValuePair<TKey, TValue>> items) {
        foreach (KeyValuePair<TKey, TValue> item in items)
            self.Add(item);
    }
}