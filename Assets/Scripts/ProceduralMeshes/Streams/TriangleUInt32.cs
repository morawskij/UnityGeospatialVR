using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace ProceduralMeshes.Streams {

	[StructLayout(LayoutKind.Sequential)]
	public struct TriangleUInt32 {
		
		public int a, b, c;

		public static implicit operator TriangleUInt32 (int3 t) => new TriangleUInt32 {
			a = t.x,
			b = t.y,
			c = t.z
		};
	}
}