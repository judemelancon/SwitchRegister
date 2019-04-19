<Query Kind="Program">
  <NuGetReference>Magick.NET-Q8-AnyCPU</NuGetReference>
  <Namespace>ImageMagick</Namespace>
</Query>

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Program mode.
// It uses the NuGet feature that requires a Developer or Premium license.
// Alternatively, it could be translated to a console program easily enough.

// Configuration
private const string SourceDirectory = null;
private const string DestinationDirectory = SourceDirectory;
// I found that I used more slight tints than I realized.
private const double SaturationThreshold = 0.1;
// End of Configuration



// https://abc.useallfive.com/?colors[]=000000,747474,757575,767676,777777,FFFFFF
private const byte DarkAccessibleGrey = 117;
private const byte LightAccessibleGrey = 118;
private const double MedianAccessibleGrey = 0.5*(DarkAccessibleGrey + LightAccessibleGrey);

void Main() {
    if (string.IsNullOrWhiteSpace(SourceDirectory))
        throw new Exception($"{nameof(SourceDirectory)} must be set");
    if (string.IsNullOrWhiteSpace(DestinationDirectory))
        throw new Exception($"{nameof(DestinationDirectory)} must be set");
    if (!SourceDirectory.EndsWith(@"\"))
        throw new Exception($"{nameof(SourceDirectory)} needs a trailing slash");

    MagickNET.Version.Dump();

    Directory.CreateDirectory(DestinationDirectory);

    foreach (string sourceFilename in Directory.EnumerateFiles(SourceDirectory, "*.png", SearchOption.AllDirectories)) {
        string relativePath = sourceFilename.Substring(SourceDirectory.Length);
        relativePath.Dump();
        try {
            string destinationFilename = Path.Combine(DestinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFilename));
            SendLowSaturationColorsToAccessibleGrey(sourceFilename, destinationFilename, SaturationThreshold);
        }
        catch (Exception ex) {
            Util.Highlight($" failed with {ex.GetType()}: {ex.Message}").Dump();
        }
    }
    // TODO GIF support
}

public static void SendLowSaturationColorsToAccessibleGrey(string sourceFilename, string destinationFilename, double saturationThreshold) {
    using (IMagickImage image = new MagickImage(sourceFilename)) {
        SendLowSaturationColorsToAccessibleGrey(image, saturationThreshold);
        image.Write(destinationFilename);
    }
}

public static void SendLowSaturationColorsToAccessibleGrey(IMagickImage image, double saturationThreshold) {
    IReadOnlyDictionary<PixelChannel, int> channelIndex = image.Channels
                                                               .Select((pc, i) => (Channel: pc, Index: i))
                                                               .ToDictionary(t => t.Channel, t => t.Index);
    if (!ConfirmExpectedChannels(image, channelIndex)) {
        image.Channels.Dump("Unhandled channel combination");
        throw new Exception();
    }

    using (IPixelCollection pixels = image.GetPixels()) {
        foreach (Pixel pixel in pixels) {
            if (pixel[channelIndex[PixelChannel.Alpha]] == 0)
                continue;

            double grey;
            if (channelIndex.Count == 4) {
                byte r = pixel[channelIndex[PixelChannel.Red]];
                byte g = pixel[channelIndex[PixelChannel.Green]];
                byte b = pixel[channelIndex[PixelChannel.Blue]];
                int minimumChannel = Math.Min(r, Math.Min(g, b));
                int maximumChannel = Math.Max(r, Math.Max(g, b));
                if (minimumChannel < 255 && maximumChannel > 0) {
                    double saturation = (maximumChannel - minimumChannel) / (255.0 - Math.Abs(maximumChannel + minimumChannel - 255.0));
                    if (saturation > saturationThreshold)
                        continue;
                }

                grey = 0.21 * r + 0.72 * g + 0.07 * b;
            }
            else {
                grey = pixel[channelIndex[PixelChannel.Gray]];
            }

            SetPixelToGrey(channelIndex, pixel, grey <= MedianAccessibleGrey ? DarkAccessibleGrey : LightAccessibleGrey);
        }
    }
}

private static bool ConfirmExpectedChannels(IMagickImage image, IReadOnlyDictionary<PixelChannel, int> channelIndex) {
    switch (image.ChannelCount) {
        case 2:
            return channelIndex.ContainsKey(PixelChannel.Alpha)
                   && channelIndex.ContainsKey(PixelChannel.Gray);
        case 4:
            return channelIndex.ContainsKey(PixelChannel.Alpha)
                   && channelIndex.ContainsKey(PixelChannel.Red)
                   && channelIndex.ContainsKey(PixelChannel.Green)
                   && channelIndex.ContainsKey(PixelChannel.Blue);
        default:
            return false;
    }
}

private static void SetPixelToGrey(IReadOnlyDictionary<PixelChannel, int> channelIndex, Pixel pixel, byte grey) {
    if (channelIndex.Count == 4) {
        pixel[channelIndex[PixelChannel.Red]] = grey;
        pixel[channelIndex[PixelChannel.Green]] = grey;
        pixel[channelIndex[PixelChannel.Blue]] = grey;
    }
    else {
        pixel[channelIndex[PixelChannel.Gray]] = grey;
    }
}