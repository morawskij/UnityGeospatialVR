using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

	public struct SquareGridNoDuplicates : IMeshGenerator {

		public int ResolutionX { get; set; }
		public int ResolutionZ { get; set; }
		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public float VE { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float dem_resolution_x { get; set; }
		public float dem_resolution_y { get; set; }
        public int VertexCount => ResolutionX*ResolutionZ;
		public int IndexCount => 6*(ResolutionX-1)*(ResolutionZ-1);
		public int JobLength => ResolutionZ;
		public float dTZ => 1f/(float)(ResolutionZ-1);
		public float dTX => 1f/(float)(ResolutionX-1);
		public float ScaleFactor => 1f/math.sqrt(ResolutionX*ResolutionX+ResolutionZ*ResolutionZ);
		public float VerticalScaleFactor => ScaleFactor*VE/dem_resolution_x;
		public float ZScaleFactor => ScaleFactor*dem_resolution_y/dem_resolution_x;

		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			int vi = ResolutionX*z, ti = 2*(ResolutionX-1)*(z-1);
			float baseTexX = 0f;
			float baseTexZ = (float)z*dTZ;
			for (int x = 0; x < ResolutionX; x++, vi ++, ti += 2, baseTexX+=dTX) {
				var vertex = new Vertex();
				vertex.position.x = (float)x*ScaleFactor;
				vertex.position.z = (float)z*ZScaleFactor;
				vertex.position.y = (elevationArray[z*ResolutionX + x]-midH)*VerticalScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(baseTexX,baseTexZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
				if (x>0 && z>0) {
					int cellStart = vi - ResolutionX - 1;
					streams.SetTriangle(ti-2, cellStart + int3(0, ResolutionX, 1));
					streams.SetTriangle(ti-1, cellStart + int3(1, ResolutionX, ResolutionX+1));
				}
			}
		}
			public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(ResolutionX*ScaleFactor, hRange*VerticalScaleFactor, ResolutionZ*ZScaleFactor));
	}
}