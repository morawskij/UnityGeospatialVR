using UnityEngine;
using Unity.Collections;					

namespace ProceduralMeshes {

	public interface IMeshGenerator {

		int VertexCount { get; }
		int IndexCount { get; }
		int JobLength { get; }
		Bounds Bounds { get; }
		int ResolutionX { get; set; }
		int ResolutionZ { get; set; }
		NativeArray<float> elevationArray { get; set; }
		float VE { get; set; }
		float midH { get; set; }
		float hRange { get; set; }
		float dem_resolution_x { get; set; }
		float dem_resolution_y { get; set; }

		void Execute<S> (int i, S streams) where S : struct, IMeshStreams;
		int ExecuteEdge<S> (float4 coordinateRange, float4 uvRange, bool2 stitchInfo, bool2 holeInfo, int startingVertexIndex, NativeArray<float> elevationArray, S streams) where S : struct, IMeshStreams;
		void ExecuteTile<S> (float4 coordinateRange, float4 uvRange, bool4 holes, bool2 lowerEdge, int2 lowerEdgeVertices, bool2 leftEdge, int2 leftEdgeVertices, bool2 upperEdge, int2 upperdgeVertices, bool2 rightEdge, int2 rightEdgeVertices, int startingTriangleIndex, int startingVertexIndex, S streams) where S : struct, IMeshStreams;
	}
}