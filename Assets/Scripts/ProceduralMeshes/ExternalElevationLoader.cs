using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ProceduralMeshes {

	public sealed class ExternalElevationLoader : MonoBehaviour {

		int SampleCount = (int)Math.Pow(2,16);
		public NativeArray<float> LoadTile (
			string projectName, int level, int x, int z, float midH
		) {
			string relativePath = Path.Combine(
				"Projects", projectName, level.ToString(), $"{x}_{z}.bytes"
			).Replace('\\', '/');
			string path = Path.Combine(Application.streamingAssetsPath, relativePath);
			byte[] bytes = null;
			bool hasValidTile = true;
			try {
				bytes = File.ReadAllBytes(path);
			}
			catch (Exception) {
				hasValidTile = false;
			}

			if (bytes == null || bytes.Length != SampleCount * sizeof(float)) {
				hasValidTile = false;
			}

			NativeArray<float> samples = new NativeArray<float>(SampleCount, Allocator.Persistent);
			for (int i = 0; i < SampleCount; i++) {
				samples[i] = hasValidTile
					? BitConverter.ToSingle(bytes, i * sizeof(float))
					: midH;
			}

			return samples;
		}
	}
}