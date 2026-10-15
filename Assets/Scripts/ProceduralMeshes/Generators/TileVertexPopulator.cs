using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;

using static Unity.Mathematics.math;

namespace ProceduralMeshes.Generators {

	public struct  TileVertexPopulator
    {
        public NativeArray<float> elevationArray { get; set; }
		public float VerticalScaleFactor  { get; set; }
		public float ZScaleFactor  { get; set; }
		public float midH { get; set; }
        public int Level { get; set; }
        public int2 TileNumbers { get; set; }
        public int BaseVertexIndex { get; set; }
        public float2 coordinateStart => float2((float)TileNumbers.x/(float)(1<<Level),(float)TileNumbers.y/(float)(1<<Level));
        public float2 coordinateSpan => float2(1f/(float)(1<<Level),1f/(float)(1<<Level));
        public int JobLength => 256;

        public void Execute<S> (int z, S streams) where S : struct, IMeshStreams
        {
            int vi = BaseVertexIndex + 256*z;
			for (int x = 0; x < 256; x++, vi ++) {
				var vertex = new Vertex();
				vertex.position.x = coordinateStart.x + coordinateSpan.x * (float)x / 255f;
				float fractionalZ = coordinateStart.y + coordinateSpan.y * (float)z / 255f;
				vertex.position.z = fractionalZ * ZScaleFactor;
				vertex.position.y = (elevationArray[z*256 + x]-midH)*VerticalScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(vertex.position.x,fractionalZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
			}            
         }

    }
}
