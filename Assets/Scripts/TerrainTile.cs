using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;
using static Unity.Mathematics.math;

namespace ProceduralMeshes {

	public struct TerrainTile : IMeshGeneratorTiled {

        public int level { get; set; }
		public int levelExcess {get; set;}
        public int tileX { get; set; }
        public int tileZ { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float zScaleFactor { get; set; }
		public float VerticalScaleFactor {get; set;}
		public int4 holes {get; set;}
		[field: ReadOnly]
		public NativeArray<float> elevationArray { get; set; }
		public float dT => 1f / 255f;
		float scaleFactor => 1f / Mathf.Pow(2f, level + 8);
		float scaleFactorZ => scaleFactor*zScaleFactor;
		float baseX => tileX / Mathf.Pow(2f, level);
		float baseZ => tileZ * zScaleFactor / Mathf.Pow(2f, level);
		public void Execute<S> (int z, S streams) where S : struct, IMeshStreams {
			if (level == 1)
			{
				Debug.Log(holes);
			}
			int vi = (int)Math.Min(z,128)*(257-128*holes[0]-128*holes[1]-holes[0]*holes[1])+(int)Math.Max(0,z-129)*(257-128*holes[2]-128*holes[3]-holes[2]*holes[3])+GetFromBool(z>128)*(257-128*(holes[0]*holes[2]+holes[1]*holes[3]));
			int ti = (int)Math.Min(z-1,128)*(512-256*(holes[0]+holes[1]))+(int)Math.Max(z-129,0)*(512-256*(holes[2]+holes[3]));//-256*(holes[0]+holes[1]);
			//int ti = 512*(z-1);
			int referencePower = (int)Math.Pow(2,levelExcess);
			int x0 = tileX % referencePower*(int)Math.Pow(2,8-levelExcess);
			int z0 = tileZ % referencePower*(int)Math.Pow(2,8-levelExcess);
			int x_start = 128*holes[2*GetFromBool(z>128)];
			int x_end = 257-128*holes[2*GetFromBool(z>128)+1]-holes[2*GetFromBool(z>128)]*holes[2*GetFromBool(z>128)+1];
			if (z==128)
			{
				x_start = 128*(int)holes[0]*holes[2];
				x_end = 257-128*(int)holes[1]*holes[3];
			}
			/*if (z<2 && level==1)
			{
				Debug.Log(int3(vi,x_start,x_end));
			}*/
			float baseTexX = (float)x_start*dT;
			float baseTexZ = z * dT;
			for (int x = x_start; x < x_end; x++, vi++, baseTexX += dT) {
				var vertex = new Vertex();
				vertex.position.x = baseX + x * scaleFactor;
				vertex.position.z = baseZ + z * scaleFactorZ;
				if (levelExcess > 0)
				{
					float h00 = elevationArray[(z0+(int)Math.Floor((float)z/(float)referencePower)) * 257 + x0+(int)Math.Floor((float)x/(float)referencePower)];
					float h01 = elevationArray[(z0+(int)Math.Floor((float)z/(float)referencePower)) * 257 + x0+(int)Math.Ceiling((float)x/(float)referencePower)];
					float h10 = elevationArray[(z0+(int)Math.Ceiling((float)z/(float)referencePower)) * 257 + x0+(int)Math.Floor((float)x/(float)referencePower)];
					float h11 = elevationArray[(z0+(int)Math.Ceiling((float)z/(float)referencePower)) * 257 + x0+(int)Math.Ceiling((float)x/(float)referencePower)];
					float alpha = (float)(z%referencePower)/(float)referencePower;
					float beta = (float)(x%referencePower)/(float)referencePower;
					vertex.position.y = (alpha*beta*h11+alpha*(1-beta)*h10+(1-alpha)*beta*h01+(1-alpha)*(1-beta)*h00 - midH) * VerticalScaleFactor; 
				}
				else
				{
					vertex.position.y = (elevationArray[z * 257 + x] - midH) * VerticalScaleFactor;
				}
				vertex.normal.y = 1f;
				vertex.texCoord0 = float2(baseTexX,baseTexZ);
				vertex.tangent.xw = float2(1f, -1f);
				streams.SetVertex(vi, vertex);
				/*if (z == 1 && level==1)
				{
					Debug.Log(float2((float)GetFromBool(x>128)*0.5f,(float)(2*GetFromBool(x>128))*0.5f));
					Debug.Log(int3(x,x_start,128*holes[(int)((float)(2*GetFromBool(x>128))*0.5f)]));
				}*/
				if (x>x_start && z>128*holes[(int)((float)(2*GetFromBool(x>128))*0.5f)]) {
					ti+=2;
					int row_shifter = 257-128*(GetFromBool(x>128)*GetFromBool(z!=128 || holes[0]*holes[2]==1)*holes[2*GetFromBool(z>128)]+GetFromBool(x<=128)*GetFromBool(z!=129 || holes[1]*holes[3]==1)*holes[2*(int)GetFromBool(z>129)+1]+GetFromBool(z==129)*holes[GetFromBool(x<=128)]*holes[2+GetFromBool(x<=128)]);
					int cellStart = vi -row_shifter-1;
					/*if (z==128 && level==1) // Still wrong formula, getting 257 for 129 when shouldn't
					{
						Debug.Log(int4(z,row_shifter,ti,cellStart));
					}*/
					streams.SetTriangle(ti - 2, cellStart + int3(0, row_shifter, 1));
					streams.SetTriangle(ti - 1, cellStart + int3(1, row_shifter, row_shifter+1));
				}
			}
			int GetFromBool(bool b) => b ? 1 : 0;

		}
		public Bounds Bounds => new Bounds(
			new Vector3(baseX + 128f * scaleFactor, 0f, baseZ + 128f * scaleFactorZ),
			new Vector3(256f * scaleFactor, hRange * VerticalScaleFactor, 256f * scaleFactorZ)
		);
	}
}