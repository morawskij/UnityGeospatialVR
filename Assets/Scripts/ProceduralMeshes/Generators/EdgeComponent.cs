using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

	public struct EdgeComponent : IMeshGenerator {
    	public float ScaleFactor  { get; set; }
		public float VerticalScaleFactor  { get; set; }
		public float ZScaleFactor  { get; set; }
		public float midH { get; set; }
		public int JobLength => 1;
		int ExecuteEdge<S> (float4 coordinateRange, float4 uvRange, bool2 stitchInfo, bool2 holeInfo, int startingVertexIndex, NativeArray<float> elevationArray, S streams) where S : struct, IMeshStreams {
            int2 samplingSteps = new int2(2-(int)stitchInfo[0],2-(int)stitchInfo[1]);
            float2 horizontalSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(coordinateRange.z-coordinateRange.x)/512f;
            float2 verticalSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(coordinateRange.w-coordinateRange.y)/512f;
            float2 horizontalUvSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(uvRange.z-uvRange.x)/512f;
            float2 verticalUvSteps = new float2((float)samplingSteps[0],(float)samplingSteps[1])*(uvRange.w-uvRange.y)/512f;
            int midVertexCount = (int)holeInfo[0]*128*(1+(int)stitchInfo[0]);
            int postMidVertexCount = (int)holeInfo[1]*128*(1+(int)stitchInfo[1]);
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
