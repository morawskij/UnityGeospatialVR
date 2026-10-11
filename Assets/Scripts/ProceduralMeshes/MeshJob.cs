using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace ProceduralMeshes {

	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct MeshJob<G, S> : IJobFor
		where G : struct, IMeshGenerator
		where S : struct, IMeshStreams {

    		G generator;
	    	[WriteOnly]
	    	S streams;
		    public void Execute (int i) => generator.Execute(i, streams);

            public static JobHandle ScheduleParallel (
                Mesh mesh, Mesh.MeshData meshData, int ResolutionX, int ResolutionZ, NativeArray<float> elevationArray, float VE, float midH, float hRange, float dem_resolution_x, float dem_resolution_y, JobHandle dependency
                ) {
                var job = new MeshJob<G, S>();
    			job.generator.ResolutionX = ResolutionX;
    			job.generator.ResolutionZ = ResolutionZ;
				job.generator.elevationArray = elevationArray;
				job.generator.VE = VE;
				job.generator.midH = midH;
				job.generator.hRange = hRange;
				job.generator.dem_resolution_x = dem_resolution_x;
				job.generator.dem_resolution_y = dem_resolution_y;
                job.streams.Setup(
                    meshData, mesh.bounds = job.generator.Bounds, job.generator.VertexCount, job.generator.IndexCount
                );
                return job.ScheduleParallel(job.generator.JobLength, 1, dependency);

                }

	    	}


}