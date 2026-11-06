using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace ProceduralMeshes {

	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct MeshJobTiled<G, S> : IJobFor
		where G : struct, IMeshGeneratorTiled
		where S : struct, IMeshStreams {

    		G generator;
	    	[WriteOnly]
	    	S streams;
		    public void Execute (int i) => generator.Execute(i, streams);

            public static JobHandle ScheduleParallel (
                Mesh mesh, Mesh.MeshData meshData, NativeArray<float> elevationArray, int level, int tileX, int tileZ, float midH, float hRange, float zScaleFactor, float VerticalScaleFactor, JobHandle dependency
                ) {
                var job = new MeshJobTiled<G, S>();
        		job.generator.level = level;
        		job.generator.tileX = tileX;
        		job.generator.tileZ = tileZ;
				job.generator.midH =midH;
				job.generator.hRange = hRange;
				job.generator.zScaleFactor = zScaleFactor;
				job.generator.VerticalScaleFactor = VerticalScaleFactor;
				job.generator.elevationArray = elevationArray;
                job.streams.Setup(
                    meshData, mesh.bounds = job.generator.Bounds, 66049, 393216
                );
                return job.ScheduleParallel(257, 1, dependency);

                }

	    	}


}