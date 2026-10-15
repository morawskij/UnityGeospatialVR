using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace ProceduralMeshes {

	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct MeshJobTiled<G, S> : IJobFor
		where G : struct, IMeshGenerator
		where S : struct, IMeshStreams {

    		G generator;
	    	[WriteOnly]
	    	S streams;
		    public void Execute (int i) => generator.Execute(i, streams);

            public static JobHandle ScheduleParallel (
                Mesh mesh, Mesh.MeshData meshData, int deepestLevel, int numberOfLevels, int4[] tileRanges, string projectName, float VE, float midH, float hRange, float dem_resolution_x, float dem_resolution_y, JobHandle dependency
                ) {
                var job = new MeshJobTiled<G, S>();
				job.generator.deepestLevel = deepestLevel;
				job.generator.numberOfLevels = numberOfLevels;
				job.generator.tileRanges = tileRanges;
				job.generator.projectName = projectName;
				job.generator.ZScaleFactor = dem_resolution_y/dem_resolution_x;
				job.generator.VerticalScaleFactor = VE/dem_resolution_x;
				job.generator.midH = midH;
				job.generator.hRange = hRange;
                job.streams.Setup(
                    meshData, mesh.bounds = job.generator.Bounds, job.generator.VertexCount, job.generator.IndexCount
                );
                return job.ScheduleParallel(job.generator.JobLength, 1, dependency);

                }

	    	}


}