using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

    public struct EdgeComponent {
    	public float ScaleFactor  { get; set; }
        public float VerticalScaleFactor  { get; set; }
        public float ZScaleFactor  { get; set; }
        public float midH { get; set; }
        public float4 coordinateRange { get; set; }
        public float4 uvRange { get; set; }
        public bool2 stitchInfo { get; set; }
        public bool2 holeInfo { get; set;}
        public int startingVertexIndex { get; set; }
        public NativeSlice<float> elevationArray { get; set; }
        public int ExecuteEdge<S> (S streams) where S : struct, IMeshStreams {
            int2 samplingSteps = new int2(2-Convert.ToInt32(stitchInfo[0]),2-Convert.ToInt32(stitchInfo[1]));
            float2 horizontalSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(coordinateRange.z-coordinateRange.x)/512f;
            float2 verticalSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(coordinateRange.w-coordinateRange.y)/512f;
            float2 horizontalUvSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(uvRange.z-uvRange.x)/512f;
            float2 verticalUvSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(uvRange.w-uvRange.y)/512f;
            int midVertexCount = Convert.ToInt32(holeInfo[0])*128*(1+Convert.ToInt32(stitchInfo[0]));
            int postMidVertexCount = Convert.ToInt32(holeInfo[1])*128*(1+Convert.ToInt32(stitchInfo[1]));
            for (int i = 0; i<midVertexCount; i++) {
                    var vertex = new Vertex();
                    vertex.position.x = (coordinateRange.x+(float)i*horizontalSteps[0])*ScaleFactor;
                    vertex.position.z = (coordinateRange.y+(float)i*verticalSteps[0])*ZScaleFactor;
                    vertex.position.y = (elevationArray[i*samplingSteps[0]]-midH)*VerticalScaleFactor;
                    vertex.normal.y = 1f;
                    vertex.texCoord0 = float2(uvRange.x+(float)i*horizontalUvSteps[0],uvRange.y+(float)i*verticalUvSteps[1]);
                    vertex.tangent.xw = float2(1f, -1f);
                    streams.SetVertex(startingVertexIndex+i, vertex);                
            }
            float2 baseCoordinates = new float2((coordinateRange.x+coordinateRange.z)/2f,(coordinateRange.y+coordinateRange.w)/2f); 
            float2 baseUvCoordinates = new float2((uvRange.x+uvRange.z)/2f,(uvRange.y+uvRange.w)/2f); 
            startingVertexIndex+=midVertexCount;
            for (int i = 0; i<postMidVertexCount; i++) {
                    var vertex = new Vertex();
                    vertex.position.x = (baseCoordinates.x+(float)i*horizontalSteps[1])*ScaleFactor;
                    vertex.position.z = (baseCoordinates.y+(float)i*verticalSteps[1])*ZScaleFactor;
                    vertex.position.y = (elevationArray[i*samplingSteps[1]]-midH)*VerticalScaleFactor;
                    vertex.normal.y = 1f;
                    vertex.texCoord0 = float2(uvRange.x+(float)i*horizontalUvSteps[1],uvRange.y+(float)i*verticalUvSteps[1]);
                    vertex.tangent.xw = float2(1f, -1f);
                    streams.SetVertex(startingVertexIndex+i, vertex);                
            }
            return midVertexCount+postMidVertexCount;
        }
    }
}
