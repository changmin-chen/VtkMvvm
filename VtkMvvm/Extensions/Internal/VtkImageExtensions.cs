using Kitware.VTK;

namespace VtkMvvm.Extensions.Internal
{
    internal static class VtkImageExtensions
    {
        /// <summary>
        /// Computes the lower and upper percentiles of the scalar distribution in an image.
        /// </summary>
        /// <param name="image">The VTK image to analyze.</param>
        /// <param name="lowerPercentile">Lower percentile (0–1), e.g. 0.01 for the 1st percentile.</param>
        /// <param name="upperPercentile">Upper percentile (0–1), e.g. 0.99 for the 99th percentile.</param>
        /// <param name="numBins">Number of histogram bins to use (more bins → finer resolution).</param>
        /// <param name="ignoreZero">If true, pixels with value exactly 0 are skipped (often background).</param>
        /// <returns>
        ///   A tuple (Lower, Upper) giving the scalar values at the requested percentiles.
        /// </returns>
        public static (double Lower, double Upper) GetPercentileRange(
            this vtkImageData image,
            double lowerPercentile = 0.01,
            double upperPercentile = 0.99,
            int numBins = 256,
            bool ignoreZero = false)
        {
            if (lowerPercentile < 0 || lowerPercentile > 1
                                    || upperPercentile < 0 || upperPercentile > 1
                                    || lowerPercentile >= upperPercentile)
                throw new ArgumentException("Percentiles must satisfy 0 ≤ lower < upper ≤ 1");

            // 1) Determine scalar range of the image
            var range = image.GetScalarRange();
            double minVal = range[0], maxVal = range[1];
            var binWidth = (maxVal - minVal) / (numBins - 1);

            // 2) Build the histogram
            using var accum = vtkImageAccumulate.New();
            accum.SetInput(image);
            if (ignoreZero) accum.IgnoreZeroOn();

            // tell it: bins go from binIndex=0…numBins-1, each spaced binWidth apart
            accum.SetComponentExtent(0, numBins - 1, 0, 0, 0, 0);
            accum.SetComponentOrigin(minVal, 0, 0);
            accum.SetComponentSpacing(binWidth, 0, 0);
            accum.Update();

            // 3) Fetch the histogram and total count
            var hist = accum.GetOutput()
                .GetPointData()
                .GetScalars();
            double totalCount = accum.GetVoxelCount();

            // 4) Walk the histogram until we hit each percentile
            double cumsum = 0;
            var targetLow = totalCount * lowerPercentile;
            var targetHigh = totalCount * upperPercentile;

            int lowBin = 0,
                highBin = numBins - 1;

            for (var bin = 0; bin < numBins; bin++)
            {
                cumsum += hist.GetTuple1(bin);
                if (lowBin == 0 && cumsum >= targetLow)
                    lowBin = bin;
                if (cumsum >= targetHigh)
                {
                    highBin = bin;
                    break;
                }
            }

            // 5) Convert bin indices back to scalar values
            var lowerValue = minVal + lowBin * binWidth;
            var upperValue = minVal + highBin * binWidth;

            return (lowerValue, upperValue);
        }

        /// <summary>
        ///     The math is simple: index = (world - origin) / spacing.
        /// </summary>
        /// <returns>The transform which can transform the world coordinate to this image's ijk indices</returns>
        public static vtkMatrix4x4 GetWorldToIjkTransform(this vtkImageData image)
        {
            var origin = image.GetOrigin();
            var spacing = image.GetSpacing();

            var worldToIjk = vtkMatrix4x4.New();
            worldToIjk.Identity();

            worldToIjk.SetElement(0, 0, 1.0 / spacing[0]);
            worldToIjk.SetElement(0, 1, 0.0);
            worldToIjk.SetElement(0, 2, 0.0);
            worldToIjk.SetElement(0, 3, -origin[0] / spacing[0]);

            worldToIjk.SetElement(1, 0, 0.0);
            worldToIjk.SetElement(1, 1, 1.0 / spacing[1]);
            worldToIjk.SetElement(1, 2, 0.0);
            worldToIjk.SetElement(1, 3, -origin[1] / spacing[1]);

            worldToIjk.SetElement(2, 0, 0.0);
            worldToIjk.SetElement(2, 1, 0.0);
            worldToIjk.SetElement(2, 2, 1.0 / spacing[2]);
            worldToIjk.SetElement(2, 3, -origin[2] / spacing[2]);

            // The bottom row remains [0, 0, 0, 1]
            worldToIjk.SetElement(3, 0, 0.0);
            worldToIjk.SetElement(3, 1, 0.0);
            worldToIjk.SetElement(3, 2, 0.0);
            worldToIjk.SetElement(3, 3, 1.0);

            return worldToIjk;
        }
    }
}