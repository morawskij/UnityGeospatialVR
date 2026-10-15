using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;
using System.Collections;

public class NestedLoDs
{
        public string projectName { get; set; }
        public float VerticalScaleFactor { get; set; }
        public float ZScaleFactor { get; set; }
        public float midH { get; set; }
		public float hRange { get; set; }
        public int deepestLevel { get; set; }
        public int numberOfLevels { get; set; }
        public int4[] tileRanges { get; set; }
        public int VertexCount
        {
            get
            {
                int total = 0;
                int4 innerRange = new int4(
                    tileRanges[0].z, tileRanges[0].w,
                    tileRanges[0].z, tileRanges[0].w
                );

                for (int i = 0; i < numberOfLevels; i++)
                {
                    int4 range = tileRanges[i];
                    int tileCount = (range.z - range.x) * (range.w - range.y);
                    int innerTileCount =
                        (innerRange.z - innerRange.x) * (innerRange.w - innerRange.y);

                    total += 256 * 256 * (tileCount - innerTileCount);
                    innerRange = range / 2;
                }

                return total;
            }
        }
        public int IndexCount
        {
            get
            {
                int triangleCount = 0;
                int4 innerRange = new int4(
                    tileRanges[0].z, tileRanges[0].w,
                    tileRanges[0].z, tileRanges[0].w
                );

                for (int i = 0; i < numberOfLevels; i++)
                {
                    int4 range = tileRanges[i];
                    int tileSpanX = range.z - range.x;
                    int tileSpanY = range.w - range.y;
                    int innerSpanX = innerRange.z - innerRange.x;
                    int innerSpanY = innerRange.w - innerRange.y;

                    triangleCount +=
                        512 * tileSpanX * (256 * (tileSpanY - innerSpanY) - 2) +
                        1024 * tileSpanX +
                        512 * innerSpanX +
                        (256 * innerSpanY - 1) *
                        (2 + 512 * (tileSpanX - innerSpanX));

                    innerRange = range / 2;
                }

                return triangleCount * 3;
            }
        }
        public void Execute<S> (S streams) where S : struct, IMeshStreams
        {
            int4 innerTileRange = new int4(tileRanges[0].z, tileRanges[0].w, tileRanges[0].z, tileRanges[0].w);
            int4 innerInnerTileRange = 2*innerTileRange;
            int startingVertexIndex = 0;
            int startingTriangleIndex = 0;
            int innerStartingVertexIndex = 0;
            for (int i = 0; i < numberOfLevels; i++)
            {
                var RectangularLoD = new RectangularLoD
                {
                    VerticalScaleFactor = VerticalScaleFactor,
                    ZScaleFactor = ZScaleFactor,
                    midH = midH,
                    level = i,
                    projectName = projectName,
                    tileRange = tileRanges[i],
                    innerTileRange = innerTileRange,
                    innerInnerTileRange = innerInnerTileRange,
                    startingVertexIndex = startingVertexIndex,
                    startingTriangleIndex = startingTriangleIndex,
                    innerStartingVertexIndex = innerStartingVertexIndex
                }.Execute(streams);
                innerStartingVertexIndex = startingVertexIndex;
                startingVertexIndex += 256*256*((tileRanges[i].z-tileRanges[i].x)*(tileRanges[i].w-tileRanges[i].y)-(innerTileRange.z-innerTileRange.x)*(innerTileRange.w-innerTileRange.y));
                startingTriangleIndex += 512*(RectangularLod.tileSpans.x)*(256*(RectangularLoD.tileSpans.y-RectangularLoD.innerTileSpans.y)-2)+1024*RectangularLod.tileSpans.x+512*RectangularLoD.innerTileSpans.x+(256*RectangularLoD.innerTileSpans.y-1)*(2+512*(RectangularLoD.tileSpans.x-RectangularLoD.innerTileSpans.x));
                innerTileRange = tileRanges[i]/2;
                innerInnerTileRange = innerTileRange;
            }
        }   
        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, hRange*VerticalScaleFactor, ZScaleFactor));
}
