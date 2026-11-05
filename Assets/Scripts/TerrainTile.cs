using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;

using static Unity.Mathematics.math;

namespace ProceduralMeshes {

	public struct TerrainTile : ITiledMeshGenerator {

        public int level { get; set; }
        public int tileX { get; set; }
        public int tileZ { get; set; }
		public int ResolutionX { get; set; }
		public int ResolutionZ { get; set; }
		public float VE { get; set; }
		public float dem_resolution_x { get; set; }
		public float dem_resolution_y { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float zScaleFactor { get; set; }
		public float VerticalScaleFactor {get; set;}
		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public int VertexCount => ResolutionX * ResolutionZ;
		public int IndexCount => 6 * (ResolutionX - 1) * (ResolutionZ - 1);
		public int JobLength => ResolutionZ;
		public float dT => 1f / (ResolutionX - 1);
		public float ZScaleFactor => scaleFactor * zScaleFactor;

		float scaleFactor => 1f / Mathf.Pow(2f, level + 8);
		float baseX => tileX / Mathf.Pow(2f, level);
		float baseZ => tileZ * zScaleFactor / Mathf.Pow(2f, level);
		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			int vi = ResolutionX * z, ti = 2 * (ResolutionX - 1) * (z - 1);
			float baseTexX = 0f;
			float baseTexZ = z * dT;
			for (int x = 0; x < ResolutionX; x++, vi++, ti += 2, baseTexX += dT) {
				var vertex = new Vertex();
				vertex.position.x = baseX + x * scaleFactor;
				vertex.position.z = baseZ + z * ZScaleFactor;
				vertex.position.y = (elevationArray[z * ResolutionX + x] - midH) * VerticalScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(baseTexX,baseTexZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
				if (x>0 && z>0) {
					int cellStart = vi - ResolutionX - 1;
					streams.SetTriangle(ti - 2, cellStart + int3(0, ResolutionX, 1));
					streams.SetTriangle(ti - 1, cellStart + int3(1, ResolutionX, ResolutionX + 1));
				}
			}
		}
		public Bounds Bounds => new Bounds(
			new Vector3(baseX + ResolutionX * scaleFactor / 2f, 0f, baseZ + ResolutionZ * ZScaleFactor / 2f),
			new Vector3(ResolutionX * scaleFactor, hRange * VerticalScaleFactor, ResolutionZ * ZScaleFactor)
		);
	}
}