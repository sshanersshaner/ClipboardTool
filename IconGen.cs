using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

class IconGen
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            string svgPath = "M4 17.162l-2 .838v-12.972l12-5.028v2.507l-10 4.19v10.465z M22 6l-12 5.028v12.972l12-5.028v-12.972z M8 9.697l10-4.19v-2.507l-12 5.028v12.972l2-.838v-10.465z";
            string outFile = "app.ico";
            int[] sizes = { 256, 48, 32, 16 };

            var geometry = Geometry.Parse(svgPath);
            geometry.Freeze();

            var pngDataList = new System.Collections.Generic.List<byte[]>();
            var sizeList = new System.Collections.Generic.List<int>();

            foreach (int size in sizes)
            {
                var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    double scale = (double)size / 24.0;
                    var transform = new MatrixTransform(scale, 0, 0, scale, 0, 0);
                    dc.PushTransform(transform);
                    dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0x2B, 0x7B, 0xFF)), null, geometry);
                    dc.Pop();
                }
                bmp.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    pngDataList.Add(ms.ToArray());
                    sizeList.Add(size);
                }
            }

            // Write ICO file
            using (var fs = new FileStream(outFile, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int count = pngDataList.Count;
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)count);

                int offset = 6 + 16 * count;

                for (int i = 0; i < count; i++)
                {
                    int s = sizeList[i];
                    byte b = (byte)(s >= 256 ? 0 : s);
                    bw.Write(b);
                    bw.Write(b);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write((uint)pngDataList[i].Length);
                    bw.Write((uint)offset);
                    offset += pngDataList[i].Length;
                }

                foreach (var data in pngDataList)
                {
                    bw.Write(data);
                }
            }

            Console.WriteLine("ICO generated: " + outFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
