using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace D2RModding_SpriteEdit
{
    static class CLI
    {
        public static string AddSuffix(string filename, string suffix)
        {
            string fDir = Path.GetDirectoryName(filename);
            string fName = Path.GetFileNameWithoutExtension(filename);
            string fExt = Path.GetExtension(filename);
            return Path.Combine(fDir, String.Concat(fName, suffix, fExt));
        }
        public static Image LoadSpriteToBitmap(string spritePath, out int width, out int height, out uint frameCount) {
            var bytes = File.ReadAllBytes(spritePath);
            if (bytes == null) {
                Console.WriteLine("Unable to read " + spritePath);
                width = height = 0; frameCount = 0;
                return null;
            }

            int x, y;
            var version = BitConverter.ToUInt16(bytes, 4);
            width = BitConverter.ToInt32(bytes, 8);
            height = BitConverter.ToInt32(bytes, 0xC);
            frameCount = BitConverter.ToUInt32(bytes, 0x14);
            var bmp = new Bitmap(width, height);

            if (version == 31)
            {   // regular RGBA
                for (x = 0; x < height; x++)
                {
                    for (y = 0; y < width; y++)
                    {
                        var baseVal = 0x28 + x * 4 * width + y * 4;
                        bmp.SetPixel(y, x, Color.FromArgb(bytes[baseVal + 3], bytes[baseVal + 0], bytes[baseVal + 1], bytes[baseVal + 2]));
                    }
                }
            }
            else if (version == 61)
            {   // DXT
                var tempBytes = new byte[width * height * 4];
                Dxt.DxtDecoder.DecompressDXT5(bytes, width, height, tempBytes);
                for (y = 0; y < height; y++)
                {
                    for (x = 0; x < width; x++)
                    {
                        var baseVal = (y * width) + (x * 4);
                        bmp.SetPixel(x, y, Color.FromArgb(tempBytes[baseVal + 3], tempBytes[baseVal], tempBytes[baseVal + 1], tempBytes[baseVal + 2]));
                    }
                }
            }
            return bmp;
        }
        public static bool SaveAtlasImageToSprite(Image img, string spritePath, uint fc = 1) {
            if (fc == 0) fc = 1;
            var f = File.Open(spritePath, FileMode.OpenOrCreate, FileAccess.Write);
            if (f is null) {
                Console.WriteLine("Unable to write to " + spritePath);
                return false;
            }

            f.Write(new byte[] { (byte)'S', (byte)'p', (byte)'A', (byte)'1' }, 0, 4);
            f.Write(BitConverter.GetBytes((ushort)31), 0, 2);
            f.Write(BitConverter.GetBytes((ushort)img.Width / fc), 0, 2);
            f.Write(BitConverter.GetBytes((Int32)img.Width), 0, 4);
            f.Write(BitConverter.GetBytes((Int32)img.Height), 0, 4);
            f.Seek(0x14, SeekOrigin.Begin);
            f.Write(BitConverter.GetBytes((UInt32)fc), 0, 4);
            int x, y;
            Bitmap bmp = new Bitmap(img);
            f.Seek(0x28, SeekOrigin.Begin);
            for (x = 0; x < img.Height; x++)
            {
                for (y = 0; y < img.Width; y++)
                {
                    var pixel = bmp.GetPixel(y, x);
                    f.Write(new byte[] { pixel.R, pixel.G, pixel.B, pixel.A }, 0, 4);
                }
            }
            f.Close();
            return true;
        }
        public static void SaveAtlasImageToSpriteWithLowEnd(Image img, string spritePath, uint frameCount = 1) {
            SaveAtlasImageToSprite(img, spritePath, frameCount);
            string lowend = ".lowend";
            var spriteLowName = String.Format("{0}{1}{2}", Path.GetFileNameWithoutExtension(spritePath), lowend, Path.GetExtension(spritePath));
            var spriteLowPath = Path.Combine(Path.GetDirectoryName(spritePath), spriteLowName);
            SaveAtlasImageToSprite(ResizeBitmap(img, img.Width / 2, img.Height / 2), spriteLowPath, frameCount);
        }
        public static Image ResizeBitmap(Image image, int width, int height) {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            // Workaround Mono bug: Image.HorizontalResolution/VerticalResolution could be 0.
            destImage.SetResolution(Math.Max(image.HorizontalResolution , 96) , Math.Max(image.VerticalResolution, 96));

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
        public static void MassConvertSpritesToAtlasImages(string[] spritePaths, string format, string outputImageDir = null) {
            if (spritePaths.Length == 0) return;
            var totalFilesConverted = 0;

            foreach (var spritePath in spritePaths) {
                var outputImagePath = Path.Combine(outputImageDir ?? Path.GetDirectoryName(spritePath), Path.ChangeExtension(Path.GetFileName(spritePath), format));
                var bmp = LoadSpriteToBitmap(spritePath, out _, out _, out _);
                if (bmp is null) continue;
                bmp.Save(outputImagePath);
                totalFilesConverted++;
            }

            Console.WriteLine("Converted " + totalFilesConverted + " images");
        }
        public static void MassConvertAtlasImagesToSprites(string[] imagePaths, string outputSpriteDir = null, uint frameCount = 1, bool saveLowEnd = true) {
            if (imagePaths.Length == 0) return;
            int numConverted = 0;

            foreach (var imagePath in imagePaths) {
                Image img = Image.FromFile(imagePath);
                if (img is null) {
                    Console.WriteLine("Unable to open " + imagePath);
                    continue;
                }
                string outputSpritePath = Path.Combine(outputSpriteDir ?? Path.GetDirectoryName(imagePath), Path.ChangeExtension(Path.GetFileName(imagePath), ".sprite"));
                if (saveLowEnd) {
                    SaveAtlasImageToSpriteWithLowEnd(img, outputSpritePath, frameCount);
                } else {
                    SaveAtlasImageToSprite(img, outputSpritePath, frameCount);
                }
                numConverted++;
            }

            Console.WriteLine("Converted " + numConverted + " images");
        }
        public static void ConvertSpriteToFrameImages(string spriteFilePath, string outputFramesPathPrefix) {
            var bmp = LoadSpriteToBitmap(spriteFilePath, out _, out _, out uint frameCount);
            int widthPerFrame = (int)(bmp.Width / frameCount);
            var outputFramesPathTemplate = Directory.Exists(outputFramesPathPrefix) ?
                    Path.Combine(outputFramesPathPrefix ?? Path.GetDirectoryName(spriteFilePath), Path.GetFileName(spriteFilePath))
                    : outputFramesPathPrefix + ".png";
            for (var i = 0; i < frameCount; i++) {
                var outputFrameFilePath = AddSuffix(outputFramesPathTemplate, "_" + i.ToString("D3"));
                Bitmap subbmp = new Bitmap(bmp).Clone(new Rectangle((int)i * widthPerFrame, 0, widthPerFrame, bmp.Height), bmp.PixelFormat);
                subbmp.Save(outputFrameFilePath);
            }
        }
        public static Image MergeFrameImagesToBitmap(string[] framePaths, out uint frameCount) {
            frameCount = 0;
            Image[] imgs = new Image[framePaths.Length];
            for (var i = 0; i < framePaths.Length; i++) {
                imgs[i] = Image.FromFile(framePaths[i]);
                if (imgs[i].Width != imgs[0].Width || imgs[i].Height != imgs[0].Height) {
                    Console.WriteLine("ERROR: Frames have different dimension");
                    return null;
                }
            }
            // make a BIG BOY image
            Bitmap bmp = new Bitmap(imgs[0].Width * imgs.Length, imgs[0].Height);
            Graphics g = Graphics.FromImage(bmp);
            g.CompositingMode = CompositingMode.SourceOver;
            for (var i = 0; i < imgs.Length; i++)
                g.DrawImage(imgs[i], new Point(imgs[0].Width * i, 0));
            frameCount = (uint)imgs.Length;
            return bmp;
        }
        public static bool ConvertFrameImagesToSprite(string spritePath, string[] framePaths) {
            var img = MergeFrameImagesToBitmap(framePaths, out uint frameCount);
            if (img is null) return false;
            SaveAtlasImageToSpriteWithLowEnd(img, spritePath, frameCount);
            return true;
        }
        public static bool RunCLI(string[] args)
        {
            if (args.Length == 0) return false;
            if (new string[] { "sprite2png", "sprite2bmp", "sprite2gif", "sprite2tiff", "sprite2jpg" }.Contains(args[0])) {
                MassConvertSpritesToAtlasImages(args.Skip(1).ToArray(), args[0].Substring(args[0].LastIndexOf('2') + 1));
            } else if(args[0] == "img2sprite") {
                MassConvertAtlasImagesToSprites(args.Skip(1).ToArray());
            } else if (args[0] == "export-frames" && args.Length >= 3) {
                ConvertSpriteToFrameImages(args[1], args[2]);
            } else if (args[0] == "import-frames" && args.Length >= 3) {
                ConvertFrameImagesToSprite(args[1], args.Skip(2).ToArray());
            } else {
                return false;
            }
            return true;
        }
    }
}
