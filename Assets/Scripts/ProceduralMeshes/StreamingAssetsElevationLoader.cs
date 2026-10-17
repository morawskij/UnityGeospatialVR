using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace ProceduralMeshes {

	public sealed class StreamingAssetsElevationLoader : MonoBehaviour {

		public const int SamplesPerSide = 256;
		public const int SampleCount = SamplesPerSide * SamplesPerSide;

		public IEnumerator LoadTile (
			string projectName, int level, int x, int z,
			Action<NativeArray<float>> onLoaded, Action<string> onError = null
		) {
			string relativePath = Path.Combine(
				"Projects", projectName, level.ToString(), $"{x}_{z}.bytes"
			).Replace('\\', '/');
			byte[] bytes = null;
			string loadError = null;

			#if UNITY_EDITOR
			string editorPath = Path.Combine(Application.streamingAssetsPath, relativePath);
			if (File.Exists(editorPath)) {
				try {
					bytes = File.ReadAllBytes(editorPath);
				}
				catch (Exception exception) {
					loadError = exception.Message;
				}
			}
			else
			#endif
			{
				string path = Path.Combine(Application.streamingAssetsPath, relativePath).Replace('\\', '/');
				using (UnityWebRequest request = UnityWebRequest.Get(path)) {
					yield return request.SendWebRequest();

					if (request.result != UnityWebRequest.Result.Success) {
						loadError = request.error;
					}
					else {
						bytes = request.downloadHandler.data;
					}
				}
			}

				if (bytes == null || bytes.Length != SampleCount * sizeof(float)) {
					onError?.Invoke(
						loadError == null
							? $"Elevation tile '{relativePath}' must contain exactly {SampleCount} float samples (256x256)."
							: $"Failed to load elevation tile '{relativePath}': {loadError}"
					);
					yield break;
				}

				NativeArray<float> samples = new NativeArray<float>(SampleCount, Allocator.Persistent);
				for (int i = 0; i < SampleCount; i++) {
					samples[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
				}

				if (onLoaded == null) {
					samples.Dispose();
					onError?.Invoke("An elevation tile callback is required.");
					yield break;
				}

				onLoaded(samples);
		}
	}
}