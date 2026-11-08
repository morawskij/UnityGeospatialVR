using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProceduralMeshes {

	public interface IMeshStreams { 

		public void Setup (Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount);
		public void SetVertex(int index, Vertex data);
		public void SetTriangle(int index, int3 triangle);

	}

}