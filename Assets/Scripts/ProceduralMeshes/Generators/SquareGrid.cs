using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

	public struct SquareGrid : IMeshGenerator {

		public int ResolutionX { get; set; }
		public int ResolutionZ { get; set; }
		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public float VE { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float dem_resolution_x { get; set; }
		public float dem_resolution_y { get; set; }
        public int VertexCount => 4*ResolutionX*ResolutionZ;
		public int IndexCount => 6*ResolutionX*ResolutionZ;
		public int JobLength => ResolutionZ;
		public float dTZ => 1f/(float)ResolutionZ;
		public float dTX => 1f/(float)ResolutionX;
		public float ScaleFactor => 1f/math.sqrt(ResolutionX*ResolutionX+ResolutionZ*ResolutionZ);
		public float VerticalScaleFactor => ScaleFactor*VE/dem_resolution_x;
		public float y_x_scale => dem_resolution_y/dem_resolution_x;

		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			int vi = 4*ResolutionX*z, ti = 2*ResolutionX*z;
			float baseTexX = 0f;
			float baseTexZ = (float)z*dTZ;
			for (int x = 0; x < ResolutionX; x++, vi += 4, ti += 2, baseTexX+=dTX) {
				var xCoordinates = float2(x, x + 1f)*ScaleFactor;
				var zCoordinates = float2(z, z + 1f)*ScaleFactor*y_x_scale;
				var yCoordinates = (float4(elevationArray[z*(ResolutionX+1) + x], elevationArray[z*(ResolutionX+1) + x + 1], elevationArray[(z + 1)*(ResolutionX+1) + x], elevationArray[(z + 1)*(ResolutionX+1) + x + 1])-midH)*VerticalScaleFactor;
				var vertex = new Vertex();
				vertex.position.x = xCoordinates.x;
				vertex.position.z = zCoordinates.x;
				vertex.position.y = yCoordinates.x;
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(baseTexX,baseTexZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
				vertex.position.x = xCoordinates.y;
				vertex.position.y = yCoordinates.y;
				vertex.texCoord0 = float2(baseTexX+dTX,baseTexZ);
				streams.SetVertex(vi+1, vertex);

				vertex.position.x = xCoordinates.x;
				vertex.position.z = zCoordinates.y;
				vertex.position.y = yCoordinates.z;
				vertex.texCoord0 = float2(baseTexX,baseTexZ+dTZ);
				streams.SetVertex(vi+2, vertex);

				vertex.position.x = xCoordinates.y;
				vertex.position.y = yCoordinates.w;
				vertex.texCoord0 = float2(baseTexX+dTX,baseTexZ+dTZ);
				streams.SetVertex(vi+3, vertex);

				streams.SetTriangle(ti, vi + int3(0, 2, 1));
				streams.SetTriangle(ti+1, vi +int3(1, 2, 3));     
			}
		}
			public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(ResolutionX*ScaleFactor, hRange*VerticalScaleFactor, ResolutionZ*ScaleFactor*y_x_scale));
	}
}