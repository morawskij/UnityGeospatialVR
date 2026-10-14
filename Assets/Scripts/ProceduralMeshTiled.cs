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
	public TextAsset elevationData;
	public TextAsset elevationMetadata;
	private Elevation elevation;
	private NativeArray<float> elevationArray;
	private float midH;
	private float hRange;
	private float dem_resolution_x;
	private float dem_resolution_y;
	public float VE = 1f;

	void Awake () {
		mesh = new Mesh {
			name = "Procedural Mesh"
		};
		Elevation elevation = new Elevation();
		elevation.Load(elevationData, elevationMetadata);
		elevationArray = elevation.values;
		midH = elevation.mid_h;
		hRange = elevation.h_range;
		dem_resolution_x = elevation.dem_resolution_x;
		dem_resolution_y = elevation.dem_resolution_y;
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

		MeshJobTiled<SquareGridNoDuplicates, SingleStream>.ScheduleParallel(
			mesh, meshData, elevationArray, VE, midH, hRange, dem_resolution_x, dem_resolution_y, default
		).Complete();

		Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
    }

}
