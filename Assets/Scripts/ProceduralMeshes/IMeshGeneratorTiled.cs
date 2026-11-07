using UnityEngine;
using Unity.Collections;	
using Unity.Mathematics;				

namespace ProceduralMeshes {

	public interface IMeshGeneratorTiled {
		Bounds Bounds { get; }
        public int level { get; set; }
		public int levelExcess {get; set;}
        public int tileX { get; set; }
        public int tileZ { get; set; }
		public float midH { get; set; }
		public float hRange { get; set; }
		public float zScaleFactor { get; set; }
		public float VerticalScaleFactor {get; set;}
		public NativeArray<float> elevationArray { get; set; }
		void Execute<S> (int i, S streams) where S : struct, IMeshStreams;
	}
}