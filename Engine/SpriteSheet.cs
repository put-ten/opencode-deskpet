using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DeskPet.Engine;

public class SpriteSheet
{
    public string Name { get; }
    public int FrameWidth { get; }
    public int FrameHeight { get; }
    public int Columns { get; private set; }
    public int FrameCount { get; private set; }
    public BitmapSource? Bitmap { get; private set; }

    public SpriteSheet(string name, int frameWidth, int frameHeight)
    {
        Name = name;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        Columns = 1;
        FrameCount = 1;
    }

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Sprite sheet file not found: {filePath}", filePath);
        var uri = new Uri(filePath, UriKind.Absolute);
        Bitmap = new BitmapImage(uri);
        Columns = Bitmap.PixelWidth / FrameWidth;
        var rows = Bitmap.PixelHeight / FrameHeight;
        FrameCount = Columns * rows;
    }

    public Int32Rect GetSourceRect(int frameIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, FrameCount);
        var col = frameIndex % Columns;
        var row = frameIndex / Columns;
        return new Int32Rect(col * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
    }
}
