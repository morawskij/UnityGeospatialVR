using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

	public struct TileComponent {

		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public float ScaleFactor  { get; set; }
		public float VerticalScaleFactor  { get; set; }
		public float ZScaleFactor  { get; set; }
		public float midH { get; set; }
		public int JobLength => 1;

		public void ExecuteTile<S> (float4 coordinateRange, float4 uvRange, bool4 holes, bool2 lowerEdge, int2 lowerEdgeVertices, bool2 leftEdge, int2 leftEdgeVertices, bool2 upperEdge, int2 upperdgeVertices, bool2 rightEdge, int2 rightEdgeVertices, int startingTriangleIndex, int startingVertexIndex, NativeArray<float> elevationArray, S streams) where S : struct, IMeshStreams {
			int currentVertexIndex = startingVertexIndex;
			int currentTriangleIndex = startingTriangleIndex;
			var edgeGenerator = new EdgeComponent {
				ScaleFactor = ScaleFactor,
				VerticalScaleFactor = VerticalScaleFactor,
				ZScaleFactor = ZScaleFactor,
				midH = midH
			};
			edgeGenerator.coordinateRange = new float4(coordinateRange.x, coordinateRange.y, coordinateRange.z, coordinateRange.y);
			edgeGenerator.uvRange = new float4(uvRange.x, uvRange.y, uvRange.z, uvRange.y);
			edgeGenerator.stitchInfo = lowerEdge;
			edgeGenerator.holeInfo = new bool2(holes.x, holes.y);
			edgeGenerator.startingVertexIndex = currentVertexIndex;
			edgeGenerator.elevationArray = new NativeSlice<float>(elevationArray, 0, 512);
			int lowerEdgeVertexCount = edgeGenerator.ExecuteEdge(streams);
			currentVertexIndex+=lowerEdgeVertexCount;
			edgeGenerator.coordinateRange = new float4(coordinateRange.x, coordinateRange.y, coordinateRange.x, coordinateRange.w);
			edgeGenerator.uvRange = new float4(uvRange.x, uvRange.y, uvRange.x, uvRange.w);
			edgeGenerator.stitchInfo = leftEdge;
			edgeGenerator.holeInfo = new bool2(holes.x, holes.z);
			edgeGenerator.startingVertexIndex = currentVertexIndex;
			edgeGenerator.elevationArray = new NativeSlice<float>(elevationArray).SliceWithStride<float>(512 * sizeof(float));
			int leftEdgeVertexCount = edgeGenerator.ExecuteEdge(streams);
			currentVertexIndex+=leftEdgeVertexCount;
			edgeGenerator.coordinateRange = new float4(coordinateRange.x, coordinateRange.w, coordinateRange.z, coordinateRange.w);
			edgeGenerator.uvRange = new float4(uvRange.x, uvRange.w, uvRange.z, uvRange.w);
			edgeGenerator.stitchInfo = upperEdge;
			edgeGenerator.holeInfo = new bool2(holes.z, holes.w);
			edgeGenerator.startingVertexIndex = currentVertexIndex;
			edgeGenerator.elevationArray = new NativeSlice<float>(elevationArray, 512 * 511, 512);
			int upperEdgeVertexCount = edgeGenerator.ExecuteEdge(streams);
			currentVertexIndex+=upperEdgeVertexCount;
			edgeGenerator.coordinateRange = new float4(coordinateRange.z, coordinateRange.y, coordinateRange.z, coordinateRange.w);
			edgeGenerator.uvRange = new float4(uvRange.z, uvRange.y, uvRange.z, uvRange.w);
			edgeGenerator.stitchInfo = rightEdge;
			edgeGenerator.holeInfo = new bool2(holes.y, holes.w);
			edgeGenerator.startingVertexIndex = currentVertexIndex;
			edgeGenerator.elevationArray = new NativeSlice<float>(elevationArray, 511, elevationArray.Length - 511).SliceWithStride<float>(512 * sizeof(float));
			int rightEdgeVertexCount = edgeGenerator.ExecuteEdge(streams);
            currentVertexIndex+=rightEdgeVertexCount;
			float horizontalStep = (coordinateRange.z-coordinateRange.x)/512f;
            float verticalStep = (coordinateRange.w-coordinateRange.y)/512f;
            float horizontalUvStep = (uvRange.z-uvRange.x)/512f;
            float verticalUvStep = (uvRange.w-uvRange.y)/512f;
			bool upperPart = false;
			bool lowerPart = true;
			bool leftPart = true;
			bool rightPart = false;
			int z00 = 256*(1-Convert.ToInt32(holes.x));
			int z01 = 256*(1-Convert.ToInt32(holes.y));
			for (int z = 1; z < 511; z++) {
				int x0=256*(1-Convert.ToInt32(lowerPart)*Convert.ToInt32(holes.x)-Convert.ToInt32(upperPart)*Convert.ToInt32(holes.z));
				for (int x = x0; x < 512-256*(1-Convert.ToInt32(lowerPart)*Convert.ToInt32(holes.y)-Convert.ToInt32(upperPart)*Convert.ToInt32(holes.w)); x++, currentVertexIndex++) {
					var vertex = new Vertex();
					vertex.position.x = (coordinateRange.x + (float)x*horizontalStep) * ScaleFactor;
					vertex.position.z = (coordinateRange.y + (float)z*verticalStep) * ZScaleFactor;
					vertex.position.y = (elevationArray[z*512 + x]-midH)*VerticalScaleFactor;
					vertex.normal.y = 1f;
					vertex.texCoord0 = float2(uvRange.x+(float)x*horizontalUvStep,uvRange.y+(float)z*verticalUvStep);
					vertex.tangent.xw = float2(1f, -1f);
					streams.SetVertex(currentVertexIndex, vertex);
					if (x>x0 && z>z00*Convert.ToInt32(leftPart)+z01*Convert.ToInt32(rightPart)) {
						int vertexShift = 256*(Convert.ToInt32(lowerPart)*Convert.ToInt32(leftPart)*Convert.ToInt32(holes.x)+Convert.ToInt32(lowerPart)*Convert.ToInt32(rightPart)*Convert.ToInt32(holes.y)+Convert.ToInt32(upperPart)*Convert.ToInt32(leftPart)*Convert.ToInt32(holes.z)+Convert.ToInt32(upperPart)*Convert.ToInt32(rightPart)*Convert.ToInt32(holes.w));
						streams.SetTriangle(currentTriangleIndex, currentVertexIndex + int3(-vertexShift-1, -1, -vertexShift));
						streams.SetTriangle(currentTriangleIndex+1, currentVertexIndex + int3(-vertexShift, -1, 0));
						currentTriangleIndex+=2;
					}
					if (x==255){
						leftPart=false;
						rightPart=true;
					}
				}
				if (z==255) {
					lowerPart=false;
					upperPart=true;
				}
			}
		}
	}
}
