using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;

using static Unity.Mathematics.math;

namespace ProceduralMeshes {

	public struct TerrainTile : IMeshGeneratorTiled {

        public int level { get; set; }
        public int tileX { get; set; }
        public int tileZ { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float zScaleFactor { get; set; }
		public float VerticalScaleFactor {get; set;}
		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public float dT => 1f / 255f;
		float scaleFactor => 1f / Mathf.Pow(2f, level + 8);
		float scaleFactorZ => scaleFactor*zScaleFactor;
		float baseX => tileX / Mathf.Pow(2f, level);
		float baseZ => tileZ * zScaleFactor / Mathf.Pow(2f, level);
		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			int vi = 257 * z, ti = 512 * (z-1);
			float baseTexX = 0f;
			float baseTexZ = z * dT;
			for (int x = 0; x < 257; x++, vi++, ti += 2, baseTexX += dT) {
				var vertex = new Vertex();
				vertex.position.x = baseX + x * scaleFactor;
				vertex.position.z = baseZ + z * scaleFactorZ;
				vertex.position.y = (elevationArray[z * 257 + x] - midH) * VerticalScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(baseTexX,baseTexZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
				if (x>0 && z>0) {
					int cellStart = vi - 258;
					streams.SetTriangle(ti - 2, cellStart + int3(0, 257, 1));
					streams.SetTriangle(ti - 1, cellStart + int3(1, 257, 258));
				}
			}

		}
		public Bounds Bounds => new Bounds(
			new Vector3(baseX + 128f * scaleFactor, 0f, baseZ + 128f * scaleFactorZ),
			new Vector3(256f * scaleFactor, hRange * VerticalScaleFactor, 256f * scaleFactorZ)
		);
	}
}