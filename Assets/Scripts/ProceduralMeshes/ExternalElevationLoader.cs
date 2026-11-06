using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ProceduralMeshes {

	public sealed class ExternalElevationLoader {

		int SampleCount = (int)Math.Pow(2,16);
		public NativeArray<float> LoadTile (
			string projectName, int level, int x, int z, float midH
		) {
			string relativePath = Path.Combine(
				"Projects", projectName, level.ToString(), $"{x}_{z}.bytes"
			).Replace('\\', '/');
			byte[] bytes = null;
			bool hasValidTile = true;
			float minimumH = 1000f;
			float maximumH = -5000f;
			try {
				bytes = File.ReadAllBytes(relativePath);
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
				if (samples[i] < minimumH) minimumH = samples[i];
				if (samples[i] > maximumH) maximumH = samples[i];
			}
			Debug.Log($"Tile {level}/{x}/{z} loaded. minH: {minimumH}, maxH: {maximumH}");

			return samples;
		}
	}
}