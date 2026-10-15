using ProceduralMeshes;
using ProceduralMeshes.Generators;
using ProceduralMeshes.Streams;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMeshTiled : MonoBehaviour
{

    private Mesh mesh;
	private ElevationMetadata elevationInfo;
	private float midH;
	private float hRange;
	private float dem_resolution_x;
	private float dem_resolution_y;
	public float VE = 1f;
	public string projectName = "Antarctica";

	void Awake () {
		mesh = new Mesh {
			name = "Procedural Mesh"
		};
		elevationInfo = new ElevationMetadata();
		elevationInfo.LoadMetadata($"Assets/StreamingAssets/Projects/{projectName}/dem_metadata.json");
		midH = elevationInfo.mid_h;
		hRange = elevationInfo.h_range;
		dem_resolution_x = elevationInfo.dem_resolution_x;
		dem_resolution_y = elevationInfo.dem_resolution_y;
		GetComponent<MeshFilter>().mesh = mesh;
	}
	
	void OnValidate () => enabled = true;

	void Update () {
		GenerateMesh();
		mesh.RecalculateNormals();
		enabled = false;
	}

	void GenerateMesh () {
		Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
		Mesh.MeshData meshData = meshDataArray[0];

		MeshJobTiled<NestedLoDs, SingleStream>.ScheduleParallel(
			mesh, meshData, VE, midH, hRange, dem_resolution_x, dem_resolution_y, default
		).Complete();

		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
    }

}
