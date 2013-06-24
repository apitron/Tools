#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Apitron.PDF.Rasterizer;
using Apitron.PDF.Rasterizer.Configuration;
using Rectangle = System.Drawing.Rectangle;

#endregion

namespace PDFCompare
{
    internal static class ImageComparer
    {
        /// <summary>
        ///   Number of errors indicating comparison should stop
        /// </summary>
        public const int ErrorLimit = 50;
        private const int AreaPixelThreshold = 4;

        /// <summary>
        ///   Represents comparizon result in a organized form
        /// </summary>
        internal class DifferenceMap
        {
            private readonly IList<Rectangle> _areas;
            private Rectangle _lastErrorArea;
            private Rectangle _catchArea;

            public DifferenceMap()
            {
                _areas = new List<Rectangle>();
                _lastErrorArea = _catchArea = new Rectangle();
            }

            public IEnumerable<Rectangle> Areas
            {
                get { return _areas; }
            }

            public void RollupErrors()
            {
                if (!_lastErrorArea.IsEmpty)
                    _areas.Add(_lastErrorArea);
            }

            //Gets number of records in difference map
            public int Size
            {
                get { return _areas.Count; }
            }

            public void AddPixel(int x, int y)
            {
                if (_catchArea.Contains(x, y))
                {
                    if (!_lastErrorArea.Contains(x, y)) // expand error area
                    {
                        _lastErrorArea = Rectangle.Union(_lastErrorArea, new Rectangle(x, y, 1, 1));
                        UpdateCatchArea();
                    }
                }
                else
                {
                    if (!_lastErrorArea.IsEmpty)
                    {
                        _areas.Add(_lastErrorArea);
                    }
                    _lastErrorArea = new Rectangle(x, y, 1, 1);
                    UpdateCatchArea();
                }
            }

            private void UpdateCatchArea()
            {
                _catchArea = _lastErrorArea;
                if (!_catchArea.IsEmpty)
                    _catchArea.Inflate(AreaPixelThreshold, 1);
            }
        }

        /// <summary>
        ///   Compares generated image with the image stored in master file.
        ///   In case of difference writes comparison map as &lt;actualFileName&gt;.compare.png
        /// </summary>
        public static void CompareImages(string masterFileName, string actualFileName)
        {
            Bitmap master;
            try
            {
                master = (Bitmap) Image.FromFile(masterFileName);
            }
            catch(FileNotFoundException ex)
            {
                Console.WriteLine("Master file {0} was not found", masterFileName);
                return ;
            }
            var actual = (Bitmap) Image.FromFile(actualFileName);

            DifferenceMap diff;
            var areIdentical = CompareImages(master, actual, 0, out diff);

            if (areIdentical) return;
            var diffMapFileName = actualFileName + ".compared.png";
            if (File.Exists(diffMapFileName))
                File.Delete(diffMapFileName);

            var diffImage = (Image) actual.Clone();
            using (var diffMapGraphics = Graphics.FromImage(diffImage))
            using (var diffAreaPen = new Pen(Color.FromArgb(128, Color.Red)))
            {
                foreach (var diffArea in diff.Areas)
                {
                    diffMapGraphics.DrawRectangle(diffAreaPen, diffArea);
                }
            }
            diffImage.Save(diffMapFileName, ImageFormat.Png);
        }

        /// <summary>
        ///   Two images comparer
        /// </summary>
        /// <returns> Gets true if images are equal </returns>
        public static bool CompareImages(Bitmap expected, Bitmap actual, int errorsLimit, out DifferenceMap diffMap)
        {
            diffMap = CompareImages(expected, actual, errorsLimit);
            return diffMap.Size == 0;
        }

        private static DifferenceMap CompareImages(Bitmap reference, Bitmap output, int errorsLimit)
        {
            var diffMap = new DifferenceMap();

            //TODO: Check if output images has the same size

            if ((int) reference.Width * (int) reference.Height > 0)
            {
                for (var y = 0; y < (int) reference.Height; y++)
                {
                    for (var x = 0; x < (int) reference.Width; x++)
                    {
                        if (reference.GetPixel(x, y) == output.GetPixel(x, y)) continue;
                        diffMap.AddPixel(x, y);
                    }
                    if (errorsLimit > 0 && diffMap.Size > errorsLimit) break;
                }
                diffMap.RollupErrors();
            }
            return diffMap;
        }

        /// <summary>
        ///   Renders the sample (pdf file) from folder specified and compares it master image located in the same folder
        ///   In case of difference, writes comparison result to output folder
        /// </summary>
        public static void ImageComparator(string folder, string sample, Size resolution)
        {
            var sampleFile = Path.Combine(folder, sample );
            var outputFile = Path.Combine(folder, sample + ".png");
            var masterFile = Path.Combine(folder, sample + ".master.png");

            using (var fs = new FileStream(sampleFile, FileMode.Open))
            {
                // it could be password protected
                var document = new Document(fs);
                for (var i = 0; i < document.Pages.Count; i++)
                {
                    Page currentPage = document.Pages[0];
                    currentPage.Render(resolution.Width, resolution.Height, new RenderingSettings()).Save(outputFile, ImageFormat.Png);
                    CompareImages(masterFile, outputFile);
                }
            }
        }
    }

    internal class PDFCompare
    {
        private static void Main(string[] args)
        {
            ImageComparer.ImageComparator(@"C:\", "3BigPreview.pdf", new Size(1200,1600));
        }
    }
}
