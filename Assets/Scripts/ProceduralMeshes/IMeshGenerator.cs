using UnityEngine;
using Unity.Collections;	
using Unity.Mathematics;				

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
	}
}