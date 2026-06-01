using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskPet.Engine;

public class PixelRenderer
{
    public int Scale { get; set; }

    public PixelRenderer(int scale = 3)
    {
        Scale = scale;
    }

    public WriteableBitmap RenderFrame(SpriteSheet sheet, int frameIndex)
    {
        var srcRect = sheet.GetSourceRect(frameIndex);
        var srcW = srcRect.Width;
        var srcH = srcRect.Height;
        var dstW = srcW * Scale;
        var dstH = srcH * Scale;
        return RenderToBuffer(sheet, srcRect, srcW, srcH, dstW, dstH);
    }

    public WriteableBitmap[] PreRender(SpriteSheet sheet)
    {
        var frames = new WriteableBitmap[sheet.FrameCount];
        for (var i = 0; i < sheet.FrameCount; i++)
        {
            var srcRect = sheet.GetSourceRect(i);
            frames[i] = RenderToBuffer(sheet, srcRect, srcRect.Width, srcRect.Height,
                srcRect.Width * Scale, srcRect.Height * Scale);
        }
        return frames;
    }

    private WriteableBitmap RenderToBuffer(SpriteSheet sheet, Int32Rect srcRect,
        int srcW, int srcH, int dstW, int dstH)
    {
        var bmp = sheet.Bitmap ?? throw new InvalidOperationException("SpriteSheet not loaded");
        var bitmap = new WriteableBitmap(dstW, dstH, 96, 96, PixelFormats.Bgra32, null);

        var srcStride = srcW * 4;
        var srcPixels = new byte[srcH * srcStride];
        bmp.CopyPixels(srcRect, srcPixels, srcStride, 0);

        var dstStride = dstW * 4;
        var dstPixels = new byte[dstH * dstStride];

        for (var y = 0; y < dstH; y++)
        {
            var srcY = y / Scale;
            for (var x = 0; x < dstW; x++)
            {
                var srcX = x / Scale;
                var srcIdx = srcY * srcStride + srcX * 4;
                var dstIdx = y * dstStride + x * 4;
                dstPixels[dstIdx + 0] = srcPixels[srcIdx + 0];
                dstPixels[dstIdx + 1] = srcPixels[srcIdx + 1];
                dstPixels[dstIdx + 2] = srcPixels[srcIdx + 2];
                dstPixels[dstIdx + 3] = srcPixels[srcIdx + 3];
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, dstW, dstH), dstPixels, dstStride, 0);
        return bitmap;
    }
}
