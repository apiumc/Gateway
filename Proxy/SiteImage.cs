using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using SkiaSharp;
using UMC.Data;
using UMC.Web;

namespace UMC.Proxy
{
    class SiteImage
    {
        public string CacheFile
        {

            get; private set;
        }
        public int Width
        {
            get; private set;
        }
        public int Height
        {
            get; private set;
        }
        public int Postion
        {
            get; private set;
        }

        SiteImage(int w, int h, int p)
        {
            this.Width = w;
            this.Height = h;
            this.Postion = p;
        }
        string _format;
        void Convert(System.IO.Stream input, System.IO.Stream output)
        {
            try
            {
                using SKCodec sKCodec = SKCodec.Create(input);
                var destImage = SKBitmap.Decode(sKCodec);
                SKCanvas graphics;
                var srcimage = CutForCustom(destImage, out graphics, this.Width, this.Height, this.Postion);
                this.Save(srcimage, sKCodec.EncodedFormat, output);
                srcimage.Dispose();
                destImage.Dispose();
                graphics.Dispose();
            }
            catch
            {
                input.CopyTo(output);
            }

        }

        public static void Convert(System.IO.Stream input, System.IO.Stream output, Web.WebMeta conf, String cacheFile)
        {
            int width = Utility.IntParse(conf["Width"], 0);
            int height = Utility.IntParse(conf["Height"], 0);
            int Postion = Utility.IntParse(conf["Postion"], 0);
            int ImageSize = Utility.IntParse(conf["ImageSize"], 5);
            int Model = Utility.IntParse(conf["Model"], 0);
            int Padding = Utility.IntParse(conf["Padding"], 10);
            var format = conf["Format"] ?? "Src";

            switch (conf["WateRmark"] ?? "None")
            {
                case "Text":
                    int FontSize = Utility.IntParse(conf["FontSize"], 5);
                    SKColor color;//= 0xfff;
                    if (SKColor.TryParse(conf["FontColor"] ?? "#8fff", out color) == false)
                    {
                        color = 0xfff;
                    }
                    var Font = conf["Font"] ?? "Arial Unicode MS";
                    var WateRmarkText = conf["WateRmarkText"];
                    if (String.IsNullOrEmpty(WateRmarkText) == false)
                    {
                        using SKTypeface sKTypeface = SKTypeface.FromFamilyName(Font) ?? SKTypeface.FromFamilyName("Arial Unicode MS");
                        new SiteImage(width, height, Model) { _format = format, CacheFile = cacheFile }.WateRmark(input, output, WateRmarkText, FontSize, sKTypeface, color, Postion, Padding);

                    }
                    else
                    {
                        new SiteImage(width, height, Model) { _format = format, CacheFile = cacheFile }.Convert(input, output);
                    }
                    break;
                case "Image":
                    var path = Data.Reflection.ConfigPath($"Static/{conf["ImagePath"]}");
                    if (System.IO.File.Exists(path))
                    {
                        using (var file = File.OpenRead(path))
                        {
                            new SiteImage(width, height, Model) { _format = format, CacheFile = cacheFile }.WateRmark(input, output, file, ImageSize, Postion, Padding);
                        };
                    }
                    else
                    {

                        new SiteImage(width, height, Model) { _format = format, CacheFile = cacheFile }.Convert(input, output);
                    }
                    break;
                default:
                    new SiteImage(width, height, Model) { _format = format, CacheFile = cacheFile }.Convert(input, output);
                    break;
            }
        }

        void WateRmark(System.IO.Stream input, System.IO.Stream output, System.IO.Stream watermark, int size, int postion, int padding)
        {
            try
            {
                using SKCodec sKCodec = SKCodec.Create(input);
                var destImage = SKBitmap.Decode(sKCodec);
                SKCanvas graphics;
                var srcimage = CutForCustom(destImage, out graphics, this.Width, this.Height, this.Postion);
                if (this.Width < 0 && this.Height < 0)
                {
                    if ((this.Width * 1000 / this.Height) == (srcimage.Width * 1000 / srcimage.Height))
                    {
                        var _watermark = SKBitmap.Decode(watermark);
                        WateRmark(graphics, srcimage.Width, srcimage.Height, _watermark, size, postion, padding);
                        _watermark.Dispose();

                    }
                }
                else
                {
                    var _watermark = SKBitmap.Decode(watermark);
                    WateRmark(graphics, srcimage.Width, srcimage.Height, _watermark, size, postion, padding);
                    _watermark.Dispose();
                }
                this.Save(srcimage, sKCodec.EncodedFormat, output);
                destImage.Dispose();
                graphics.Dispose();
                srcimage.Dispose();
            }
            catch
            {
                input.CopyTo(output);
            }
        }
        void Save(SKBitmap bitmap, SKEncodedImageFormat imageFormat, Stream output)
        {

            switch (_format)
            {
                case "gif":
                    bitmap.Encode(output, SKEncodedImageFormat.Gif, 80);
                    break;
                case "png":
                    bitmap.Encode(output, SKEncodedImageFormat.Png, 80);
                    break;
                case "webp":
                    bitmap.Encode(output, SKEncodedImageFormat.Webp, 80);
                    break;
                case "jpeg":
                    bitmap.Encode(output, SKEncodedImageFormat.Jpeg, 80);
                    break;
                case "Optimal":
                    if (String.IsNullOrEmpty(CacheFile))
                    {
                        bitmap.Encode(output, SKEncodedImageFormat.Webp, 80);
                        break;
                    }
                    else
                    {
                        using (var filePng = Utility.Writer($"{CacheFile}.png", false))
                        {
                            bitmap.Encode(filePng, SKEncodedImageFormat.Png, 80);
                        }
                        using (var filePng = Utility.Writer($"{CacheFile}.webp", false))
                        {
                            bitmap.Encode(filePng, SKEncodedImageFormat.Webp, 80);
                        }
                        try
                        {
                            AvifConverter.EncodeImage($"{CacheFile}.png", $"{CacheFile}.avif", new AvifConverterOptions() { Quality = 50 });
                        }
                        catch
                        {

                        }

                    }
                    break;
                case "avif":
                    {
                        var tempFile = System.IO.Path.GetTempFileName();
                        using (var filePng = Utility.Writer(tempFile, false))
                        {
                            bitmap.Encode(filePng, SKEncodedImageFormat.Png, 80);
                        }
                        var tempFile2 = System.IO.Path.GetTempFileName();
                        try
                        {
                            if (AvifConverter.EncodeImage(tempFile, tempFile2, new AvifConverterOptions() { Quality = 50 }).Result.Success)
                            {
                                using (var fileStream = System.IO.File.OpenRead(tempFile2))
                                {
                                    fileStream.CopyTo(output);
                                }
                            }
                        }
                        catch
                        {
                            if (String.IsNullOrEmpty(CacheFile))
                            {
                                bitmap.Encode(output, SKEncodedImageFormat.Png, 80);

                            }
                            else
                            {
                                using (var filePng = Utility.Writer($"{CacheFile}.webp", false))
                                {
                                    bitmap.Encode(filePng, SKEncodedImageFormat.Webp, 80);
                                }

                            }
                        }
                        finally
                        {
                            File.Delete(tempFile);
                            File.Delete(tempFile2);
                        }
                    }
                    break;
                default:
                case "Src":
                    bitmap.Encode(output, imageFormat, 100);
                    break;

            }



        }
        void WateRmark(System.IO.Stream input, System.IO.Stream output, String watermarkText, int size, SKTypeface family, SKColor color, int postion, int padding)
        {
            try
            {
                using SKCodec sKCodec = SKCodec.Create(input);
                var destImage = SKBitmap.Decode(sKCodec);
                SKCanvas graphics;
                var srcimage = CutForCustom(destImage, out graphics, this.Width, this.Height, this.Postion);
                if (this.Width < 0 && this.Height < 0)
                {
                    if ((this.Width * 1000 / this.Height) == (srcimage.Width * 1000 / srcimage.Height))
                    {
                        WateRmark(graphics, srcimage.Width, srcimage.Height, watermarkText, size, family, color, postion, padding);

                    }
                }
                else
                {
                    WateRmark(graphics, srcimage.Width, srcimage.Height, watermarkText, size, family, color, postion, padding);
                }
                //srcimage.Encode(output, _format ?? sKCodec.EncodedFormat, 100);

                this.Save(srcimage, sKCodec.EncodedFormat, output);
                destImage.Dispose();

                graphics.Dispose();
                srcimage.Dispose();
            }
            catch
            {
                input.CopyTo(output);
            }
        }

        static void WateRmark(SKCanvas templateG, int width, int height, SKBitmap watermark, int size, int postion, int padding)
        {
            int w = width * size / 100;
            int h = watermark.Height * w / watermark.Width;
            if (w < 5 || h < 5)
            {
                return;
            }
            int x = 0, y = 0;
            switch (postion % 9)
            {
                case 0:
                    x = padding;
                    y = padding;
                    break;
                case 1:
                    y = padding;
                    x = (width - w) / 2;
                    break;
                case 2:
                    x = width - w - padding;
                    y = padding;
                    break;
                case 3:
                    x = padding;
                    y = (height - h) / 2;
                    break;
                case 4:
                    x = (width - w) / 2;
                    y = (height - h) / 2;
                    break;
                case 5:
                    x = (width - w) - padding;
                    y = (height - h) / 2;
                    break;
                case 6:
                    x = padding;
                    y = height - h - 10;
                    break;
                case 7:
                    x = (width - w) / 2;
                    y = height - h - 10;
                    break;
                case 8:
                    x = (width - w) - padding;
                    y = height - h - 10;
                    break;
            }



            SKRect fromR = SKRect.Create(new SKPoint(0, 0), new SKSize(watermark.Width, watermark.Height));
            SKRect toR = SKRect.Create(new SKPoint(x, y), new SKSize(w, h));
            using (SKPaint paint = new SKPaint())
            {
                paint.Color = paint.Color.WithAlpha(0x88);
                paint.IsAntialias = true;
                using (var wa = watermark.Resize(new SKSizeI(w, h), SKFilterQuality.High))
                {
                    templateG.DrawBitmap(wa, toR, paint);
                }
            }

        }

        static void WateRmark(SKCanvas templateG, int width, int height, string watermarkText, int size, SKTypeface family, SKColor color, int postion, int padding)
        {
            int x = 0, y = 0;
            switch (postion % 9)
            {
                case 0:
                    x = padding;
                    y = padding + size;
                    break;
                case 1:
                    x = (width - (size * watermarkText.Length)) / 2;
                    y = padding + size;
                    break;
                case 2:
                    x = width - (size * watermarkText.Length) - padding;
                    y = padding + size;
                    break;
                case 3:
                    x = padding;
                    y = (height - size) / 2;
                    break;
                case 4:
                    x = (width - (size * watermarkText.Length)) / 2;
                    y = (height - size) / 2;
                    break;
                case 5:
                    x = (width - (size * watermarkText.Length)) - padding;
                    y = (height - size) / 2;
                    break;
                case 6:
                    x = padding;
                    y = height - padding;
                    break;
                case 7:
                    x = (width - (size * watermarkText.Length)) / 2;
                    y = height - padding;
                    break;
                case 8:
                    x = (width - (size * watermarkText.Length)) - padding;
                    y = height - padding;
                    break;
            }
            using SKFont font = new SKFont(family, size);
            using SKPaint sKPaint = new SKPaint(font);
            sKPaint.Color = color;
            sKPaint.IsAntialias = true;
            templateG.DrawText(watermarkText, x, y, sKPaint);

        }

        static SKBitmap CutForCustom(SKBitmap destImage, out SKCanvas templateG, int confWidth, int confHeight, int postion)
        {
            var maxWidth = confWidth;
            if (confWidth < 0)
            {
                if (destImage.Width >= Math.Abs(confWidth))
                {
                    maxWidth = Math.Abs(confWidth);
                }
                else
                {
                    maxWidth = 0;
                }
            }

            var maxHeight = confHeight;
            if (confHeight < 0)
            {
                var dheight = destImage.Height;
                if (maxWidth > 0)
                {
                    if (destImage.Width > maxWidth)
                    {
                        dheight = destImage.Height * maxWidth / destImage.Width;
                    }
                }
                if (dheight >= Math.Abs(confHeight))
                {
                    maxHeight = Math.Abs(confHeight);
                }
                else
                {
                    maxHeight = 0;
                }
            }
            if (maxHeight == 0 && maxWidth == 0)
            {
                templateG = new SKCanvas(destImage);
                return destImage;
            }

            bool IsThumbnail = false;
            if (maxWidth == 0)
            {
                maxWidth = destImage.Width * maxHeight / destImage.Height;
                IsThumbnail = true;
            }
            else if (maxHeight == 0)
            {
                IsThumbnail = true;
                maxHeight = destImage.Height * maxWidth / destImage.Width;
            }

            SKSize fromSize = new SKSize();
            SKSize toSize = new SKSize();
            SKPoint formP = new SKPoint();
            SKPoint toP = new SKPoint();

            if (IsThumbnail)
            {
                if (maxHeight > destImage.Height && maxWidth > destImage.Width)
                {
                    templateG = new SKCanvas(destImage);
                    return destImage;
                }
                var bmp = destImage.Resize(new SKSizeI(maxWidth, maxHeight), SKFilterQuality.High);
                templateG = new SKCanvas(bmp);
                return bmp;
            }
            if (destImage.Width < maxWidth || destImage.Height < maxHeight)
            {

                var w = destImage.Width;
                var h = maxHeight * w / maxWidth;
                var h2 = destImage.Height;
                var w2 = maxWidth * h2 / maxHeight;
                if (w > w2)
                {
                    maxWidth = w;
                    maxHeight = h;
                }
                else
                {
                    maxWidth = w2;
                    maxHeight = h2;
                }
            }
            if (postion % 4 == 0)
            {
                fromSize.Width = destImage.Width;
                fromSize.Height = destImage.Height;
                var w = fromSize.Width * maxHeight / fromSize.Height;

                if (w <= maxWidth)
                {
                    toP.X = (maxWidth - w) / 2;
                    toSize.Height = maxHeight;
                    toSize.Width = w;
                }
                else
                {
                    var h = fromSize.Height * maxWidth / fromSize.Width;
                    toSize.Width = maxWidth;
                    toP.Y = (maxHeight - h) / 2;
                    toSize.Height = h;
                }

                if (toSize.Width < fromSize.Width)
                {
                    fromSize.Height = toSize.Height;
                    fromSize.Width = toSize.Width;
                    var bmp = destImage.Resize(new SKSizeI((int)toSize.Width, (int)toSize.Height), SKFilterQuality.High);
                    destImage.Dispose();
                    destImage = bmp;

                }

                var image = new SKBitmap(maxWidth, maxHeight);
                templateG = new SKCanvas(image);

                templateG.DrawBitmap(destImage, toP.X, toP.Y);
                return image;
            }

            if (destImage.Width > maxWidth || destImage.Height > maxHeight)
            {
                var w = maxWidth;
                var h = destImage.Height * w / destImage.Width;
                var h2 = maxHeight;
                var w2 = destImage.Width * h2 / destImage.Height;
                if (w > w2)
                {
                    var bmp = destImage.Resize(new SKSizeI(w, h), SKFilterQuality.High);
                    destImage.Dispose();
                    destImage = bmp;

                }
                else
                {
                    var bmp = destImage.Resize(new SKSizeI(w2, h2), SKFilterQuality.High);
                    destImage.Dispose();
                    destImage = bmp;

                }
            }

            switch (postion % 4)
            {
                case 0:
                    break;
                case 1:
                    {
                        toSize.Width = maxWidth;
                        toSize.Height = maxHeight;
                        var w = maxWidth * destImage.Height / maxHeight;
                        if (w > destImage.Width)
                        {
                            var h = maxHeight * destImage.Width / maxWidth;
                            fromSize.Width = destImage.Width;
                            fromSize.Height = h;
                        }
                        else
                        {
                            fromSize.Width = destImage.Height;
                            fromSize.Height = w;
                        }
                    }
                    break;
                case 2:
                    {
                        toSize.Width = maxWidth;
                        toSize.Height = maxHeight;
                        var w = maxWidth * destImage.Height / maxHeight;
                        if (w > destImage.Width)
                        {
                            var h = maxHeight * destImage.Width / maxWidth;
                            fromSize.Width = destImage.Width;
                            fromSize.Height = h;
                            formP.Y = (destImage.Height - h) / 2;
                        }
                        else
                        {
                            fromSize.Width = w;
                            fromSize.Height = destImage.Height;
                            formP.X = (destImage.Width - w) / 2;
                        }
                    }
                    break;
                case 3:

                    {
                        toSize.Width = maxWidth;
                        toSize.Height = maxHeight;
                        var w = maxWidth * destImage.Height / maxHeight;
                        if (w > destImage.Width)
                        {
                            var h = maxHeight * destImage.Width / maxWidth;
                            fromSize.Width = destImage.Width;
                            fromSize.Height = h;
                            formP.Y = destImage.Height - h;
                        }
                        else
                        {
                            fromSize.Width = w;
                            fromSize.Height = destImage.Height;

                            formP.X = destImage.Width - w;
                        }
                    }
                    break;
            }

            var templateImage = new SKBitmap(maxWidth, maxHeight);
            templateG = new SKCanvas(templateImage);

            SKRect fromR = SKRect.Create(formP, fromSize);
            SKRect toR = SKRect.Create(toP, toSize);

            templateG.DrawBitmap(destImage, fromR, toR);
            return templateImage;
        }
    }
}

