using System;
using System.Collections.Generic;
using System.Linq;

namespace UltraFace;

internal static class Nms
{
    public static List<int> Run(
        IReadOnlyList<FaceDetection> detections,
        float iouThreshold)
    {
        if (detections.Count == 0) return [];

        var indices = Enumerable.Range(0, detections.Count)
            .OrderByDescending(i => detections[i].Confidence)
            .ToList();

        var kept = new List<int>(detections.Count);

        while (indices.Count > 0)
        {
            var current = indices[0];
            kept.Add(current);
            indices.RemoveAt(0);

            for (var i = indices.Count - 1; i >= 0; i--)
            {
                var other = indices[i];
                if (IoU(detections[current], detections[other]) > iouThreshold)
                    indices.RemoveAt(i);
            }
        }

        return kept;
    }

    private static float IoU(FaceDetection a, FaceDetection b)
    {
        var xLeft = Math.Max(a.X1, b.X1);
        var yTop = Math.Max(a.Y1, b.Y1);
        var xRight = Math.Min(a.X2, b.X2);
        var yBottom = Math.Min(a.Y2, b.Y2);

        var interW = Math.Max(0, xRight - xLeft);
        var interH = Math.Max(0, yBottom - yTop);
        var intersection = interW * interH;

        var areaA = Math.Max(0, a.Width) * Math.Max(0, a.Height);
        var areaB = Math.Max(0, b.Width) * Math.Max(0, b.Height);

        var union = areaA + areaB - intersection;
        if (union <= 0) return 0;

        return (float)intersection / union;
    }
}