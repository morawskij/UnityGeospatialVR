using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;
using System.Collections;

namespace ProceduralMeshes.Generators {

    public struct RectangularLoD
    {
        public int level { get; set; }
        public int4 tileRange { get; set; }
        public int4 innerRectangleTileRange { get; set; }
        public int4 innerInnerRectangleTileRange { get; set; }
        public int startingVertexIndex { get; set; }
        public int startingTriangleIndex {get; set; }
        public int innerStartingVertexIndex { get; set; }
        public string projectName { get; set; }
        public float VerticalScaleFactor { get; set; }
        public float ZScaleFactor { get; set; }
        public float midH { get; set; }
        public int2 tileSpans => int2(tileRange.z - tileRange.x, tileRange.w - tileRange.y);
        public int2 innerTileSpans => int2(innerRectangleTileRange.z - innerRectangleTileRange.x, innerRectangleTileRange.w - innerRectangleTileRange.y);
        public int tileVerticalOffset => innerRectangleTileRange.y - tileRange.y;
        public int tileHorizontalOffset => innerRectangleTileRange.x - tileRange.x;
        public int2 innerInnerTileSpans => int2(innerInnerRectangleTileRange.z - innerInnerRectangleTileRange.x, innerInnerRectangleTileRange.w - innerInnerRectangleTileRange.y);
        public int innerTileVerticalOffset => innerInnerRectangleTileRange.y - 2*innerTileRange.y;
        public int innerTileHorizontalOffset => innerInnerRectangleTileRange.x - 2*innerTileRange.x;
        public int VertexIndex (int p, int q)
        {
            int n = p/256;
            int m = q/256;
            int tau = tileSpans.x*(Math.Min(n, tileVerticalOffset) + Math.Min(0,n-tileVerticalOffset - innerTileSpans.y))+(tileSpans.x-innerTileSpans.x)*Math.Min(Math.Max(0,n-tileVerticalOffset),innerTileSpans.y)+m;
            if (n >= tileVerticalOffset && n < tileVerticalOffset + innerTileSpans.y ) {
                tau -=Math.Min(Math.Max(m-tileHorizontalOffset, 0), innerTileSpans.x);
            }
            return startingVertexIndex + tau*StreamingAssetsElevationLoader.SampleCount + (p%256)*256 + (q%256);
        }
        public int innerVertexIndex (int p, int q)
        {
            int n = p/256;
            int m = q/256;
            int tau = 2*innerTileSpans.x*(Math.Min(n, innerTileVerticalOffset) + Math.Min(0,n-innerTileVerticalOffset - 2*innerTileSpans.y))+(2*innerTileSpans.x-innerInnerTileSpans.x)*Math.Min(Math.Max(0,n-innerTileVerticalOffset),innerInnerTileSpans.y)+m;
            if (n >= innerTileVerticalOffset && n < innerTileVerticalOffset + innerInnerTileSpans.y ) {
                tau -=Math.Min(Math.Max(m-innerTileHorizontalOffset, 0), innerInnerTileSpans.x);
            }
            return innerStartingVertexIndex + tau*StreamingAssetsElevationLoader.SampleCount + (p%256)*256 + (q%256);
        }


        public IEnumerator Execute<S> (
            S streams, StreamingAssetsElevationLoader loader, Action<string> onError = null
        ) where S : struct, IMeshStreams
        {
            if (loader == null) {
                ReportError("An elevation tile loader is required.", onError);
                yield break;
            }

            int baseVertexIndex = startingVertexIndex;
            for (int z = tileRange.y; z < tileRange.w; z++) {
                for (int x = tileRange.x; x < tileRange.z; x++) {
                    if (z >= innerRectangleTileRange.y && z < innerRectangleTileRange.w &&
                        x >= innerRectangleTileRange.x && x < innerRectangleTileRange.z) {
                        continue;
                    }

                    NativeArray<float> tileElevations = default;
                    string loadError = null;
                    yield return loader.LoadTile(
                        projectName, level, x, z,
                        samples => tileElevations = samples,
                        error => loadError = error
                    );

                    if (loadError != null || !tileElevations.IsCreated) {
                        ReportError(loadError ?? $"Elevation tile {x}_{z} did not load.", onError);
                        yield break;
                    }

                    try {
                        var vertexPopulator = new TileVertexPopulator {
                            elevationArray = tileElevations,
                            VerticalScaleFactor = VerticalScaleFactor,
                            ZScaleFactor = ZScaleFactor,
                            midH = midH,
                            Level = level,
                            TileNumbers = new int2(x, z),
                            BaseVertexIndex = baseVertexIndex
                        };
                        for (int row = 0; row < StreamingAssetsElevationLoader.SamplesPerSide; row++) {
                            vertexPopulator.Execute(row, streams);
                        }
                    }
                    finally {
                        tileElevations.Dispose();
                    }

                    baseVertexIndex += StreamingAssetsElevationLoader.SampleCount;
                }
            }
            int ti = startingTriangleIndex;
            // Upper part, lower LoD
                for (int p=0; p<256*tileVerticalOffset-1; p++)
                {
                    for (int q=0; q<256*tileSpans.x; q++, ti+=2){
                        streams.SetTriangle(ti, int3(VertexIndex(p,q),VertexIndex(p+1,q+1),VertexIndex(p+1,q)));
                        streams.SetTriangle(ti+1, int3(VertexIndex(p,q),VertexIndex(p,q+1),VertexIndex(p+1,q+1)));
                    }
                }
            // Row with upper horizontal stitch
                // Left part, lower LoD
                int upperStitchP = 256*tileVerticalOffset-1;
                for (int q=0; q<256*tileHorizontalOffset-1; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(upperStitchP,q),VertexIndex(upperStitchP+1,q+1),VertexIndex(upperStitchP+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(upperStitchP,q),VertexIndex(upperStitchP,q+1),VertexIndex(upperStitchP+1,q+1)));
                }
                // Upper left corner of the transition
                int leftStitchQ = 256*tileHorizontalOffset-1;
                streams.SetTriangle(ti, int3(VertexIndex(upperStitchP,leftStitchQ),innerStartingVertexIndex,VertexIndex(upperStitchP+1,leftStitchQ)));
                streams.SetTriangle(ti+1, int3(VertexIndex(upperStitchP,leftStitchQ),VertexIndex(upperStitchP,leftStitchQ+1),innerStartingVertexIndex));
                ti+=2;
                // Upper horizontal stitch
                for (int q=leftStitchQ; q<leftStitchQ+256*innerTileSpans.x; q++, ti += 3)
                {
                    streams.SetTriangle(ti, int3(VertexIndex(upperStitchP,q),innerVertexIndex(0,2*(q-leftStitchQ)+1),innerVertexIndex(0,2*(q-leftStitchQ))));
                    streams.SetTriangle(ti+1, int3(VertexIndex(upperStitchP,q),VertexIndex(upperStitchP,q+1),innerVertexIndex(0,2*(q-leftStitchQ)+1)));
                    streams.SetTriangle(ti+2, int3(innerVertexIndex(0,2*(q-leftStitchQ)+1),VertexIndex(upperStitchP,q+1),innerVertexIndex(0,2*(q-leftStitchQ+1))));
                }
                // Upper right corner of the transition
                int rightStitchQ = 256*(tileHorizontalOffset+innerTileSpans.x)-1;
                streams.SetTriangle(ti, int3(VertexIndex(upperStitchP,rightStitchQ),VertexIndex(upperStitchP,rightStitchQ+1),innerVertexIndex(0,512*innerTileSpans.x-1)));
                streams.SetTriangle(ti+1, int3(innerVertexIndex(0,512*innerTileSpans.x-1),VertexIndex(upperStitchP,rightStitchQ+1),VertexIndex(upperStitchP+1,rightStitchQ+1)));
                ti+=2;
                // Right part, lower LoD
                for (int q=rightStitchQ+1; q<256*tileSpans.x-1; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(upperStitchP,q),VertexIndex(upperStitchP+1,q+1),VertexIndex(upperStitchP+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(upperStitchP,q),VertexIndex(upperStitchP,q+1),VertexIndex(upperStitchP+1,q+1)));
                }
            // Rows with left and right stitches to upper LoD, and inner LoD in between
            for (int p=upperStitchP+1; p<upperStitchP+256*innerTileSpans.y; p++)
            {
                // Left part, lower LoD
                for (int q=0; q<upperStitchQ; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(p,q),VertexIndex(p+1,q+1),VertexIndex(p+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(p,q),VertexIndex(p,q+1),VertexIndex(p+1,q+1)));
                }
                // Left vertical stitch
                streams.SetTriangle(ti, int3(VertexIndex(p,upperStitchQ), innerVertexIndex(2*(p-upperStitchP),0),innerVertexIndex(2*(p-upperStitchP)+1,0)));
                streams.SetTriangle(ti+1, int3(VertexIndex(p,upperStitchQ), innerVertexIndex(2*(p-upperStitchP)+1,0),VertexIndex(p+1,upperStitchQ)));
                streams.SetTriangle(ti+2, int3(innerVertexIndex(2*(p-upperStitchP)+1,0),innerVertexIndex(2*(p-upperStitchP+1),0),VertexIndex(p+1,upperStitchQ)));
                ti+=3;
                // Right vertical stitch
                int innerStitchLateQ = 512*innerTileSpans.x-1;
                streams.SetTriangle(ti, int3(innerVertexIndex(2*(p-upperStitchP),innerStitchLateQ), VertexIndex(p,rightStitchQ+1),innerVertexIndex(2*(p-upperStitchP)+1,innerStitchLateQ)));
                streams.SetTriangle(ti+1, int3(innerVertexIndex(2*(p-upperStitchP)+1,innerStitchLateQ), VertexIndex(p,rightStitchQ+1),VertexIndex(p+1,rightStitchQ+1)));
                streams.SetTriangle(ti+2, int3(innerVertexIndex(2*(p-upperStitchP)+1,innerStitchLateQ),VertexIndex(p+1,rightStitchQ+1),innerVertexIndex(2*(p-upperStitchP+1),innerStitchLateQ)));
                ti+=3;
                // Right part, lower LoD
                for (int q=rightStitchQ+1; q<256*tileSpans.x-1; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(p,q),VertexIndex(p+1,q+1),VertexIndex(p+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(p,q),VertexIndex(p,q+1),VertexIndex(p+1,q+1)));
                }
            }
            // Row with lower horizontal stitch        
                // Left part, lower LoD
                int lowerStitchP = upperStitchP+256*innerTileSpans.y;
                for (int q=0; q<256*tileHorizontalOffset-1; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(lowerStitchP,q),VertexIndex(lowerStitchP+1,q+1),VertexIndex(lowerStitchP+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(lowerStitchP,q),VertexIndex(lowerStitchP,q+1),VertexIndex(lowerStitchP+1,q+1)));
                }
                // Lower left corner of the transition
                streams.SetTriangle(ti, int3(VertexIndex(lowerStitchP,leftStitchQ),innerVertexIndex(512*innerTileSpans.y-1,0),VertexIndex(lowerStitchP+1,leftStitchQ)));
                streams.SetTriangle(ti+1, int3(innerVertexIndex(512*innerTileSpans.y-1,0),VertexIndex(lowerStitchP+1,leftStitchQ+1),VertexIndex(lowerStitchP+1,leftStitchQ)));
                ti+=2;
                // Lower horizontal stitch
                for (int q=leftStitchQ; q<leftStitchQ+256*innerTileSpans.x; q++, ti += 3)
                {
                    streams.SetTriangle(ti, int3(innerVertexIndex(512*innerTileSpans.y-1,2*(q-leftStitchQ)),innerVertexIndex(512*innerTileSpans.y-1,2*(q-leftStitchQ)+1),VertexIndex(lowerStitchP+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(lowerStitchP+1,q),innerVertexIndex(512*innerTileSpans.y-1,2*(q-leftStitchQ)+1),VertexIndex(lowerStitchP+1,q+1)));
                    streams.SetTriangle(ti+2, int3(innerVertexIndex(512*innerTileSpans.y-1,2*(q-leftStitchQ)+1),innerVertexIndex(512*innerTileSpans.y-1,2*(q-leftStitchQ+1)),VertexIndex(lowerStitchP+1,q+1)));
                }
                // Lower right corner of the transition
                streams.SetTriangle(ti, int3(innerVertexIndex(512*innerTileSpans.y-1,512*innerTileSpans.x-1),VertexIndex(lowerStitchP,rightStitchQ+1),VertexIndex(lowerStitchP+1,rightStitchQ)));
                streams.SetTriangle(ti+1, int3(VertexIndex(lowerStitchP,rightStitchQ+1),VertexIndex(lowerStitchP+1,rightStitchQ+1),VertexIndex(lowerStitchP+1,rightStitchQ)));
                ti+=2;
                // Right part, lower LoD
                for (int q=rightStitchQ+1; q<256*innerTileSpans.x-1; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(lowerStitchP,q),VertexIndex(lowerStitchP+1,q+1),VertexIndex(lowerStitchP+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(lowerStitchP,q),VertexIndex(lowerStitchP,q+1),VertexIndex(lowerStitchP+1,q+1)));
                }
            // Lower part, lower LoD
            for (int p=lowerStitchP+1; p<256*innerTileSpans.y-1; p++)
            {
                for (int q=0; q<256*tileSpans.x; q++, ti+=2){
                    streams.SetTriangle(ti, int3(VertexIndex(p,q),VertexIndex(p+1,q+1),VertexIndex(p+1,q)));
                    streams.SetTriangle(ti+1, int3(VertexIndex(p,q),VertexIndex(p,q+1),VertexIndex(p+1,q+1)));
                }
            }
        }

        static void ReportError(string message, Action<string> onError) {
            if (onError != null) {
                onError(message);
            }
            else {
                Debug.LogError(message);
            }
        }
    }
}