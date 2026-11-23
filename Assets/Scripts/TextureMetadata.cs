using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

using System.Reflection;

namespace ProceduralMeshes {

	public class AllTexturesMetadata {

        public int numberOfTextures = 0;
        public List<int> pyramidLevels;
        public List<string> textureNames;
        public List<int4> levelRanges = new List<int4>();

        public void LoadMetadataFolder(string projectName) {
            string textureMetadataPattern = $"texture*metadata.json";
            List<string> metadataFiles = new List<string>(Directory.GetFiles($"Projects/{projectName}", textureMetadataPattern));
            numberOfTextures = metadataFiles.Count-1;
            pyramidLevels = new List<int>(numberOfTextures);
            textureNames = new List<string>(numberOfTextures);
            for (int i = 0; i<numberOfTextures; i++)
            {
                TextureMetadata currentTextureMetadata = new TextureMetadata();
                currentTextureMetadata.TextureLoadMetadata($"Projects/{projectName}/texture_{i.ToString()}_metadata.json");
                textureNames.Add(currentTextureMetadata.name);
                pyramidLevels.Add(currentTextureMetadata.pyramid_levels);             
            }
        string textureFamilyMetadataSource = Path.Combine("Projects",projectName,"texture_family_metadata.json");
        TextureFamilyMetadata texturesMetadata = new TextureFamilyMetadata();
        texturesMetadata.TextureFamilyLoadMetadata(textureFamilyMetadataSource);

        for (int i=0; i<texturesMetadata.levelRanges.Length; i+=4)
        {
            levelRanges.Add(new int4(
                texturesMetadata.levelRanges[i],
                texturesMetadata.levelRanges[i+1],
                texturesMetadata.levelRanges[i+2],
                texturesMetadata.levelRanges[i+3]
            ));
        }
        }
	}

    public class TextureMetadata
    {
        public string name;
        public int pyramid_levels;

        public void TextureLoadMetadata(TextAsset metadataAsset) {
            TextureLoadMetadataJson(metadataAsset.text);
        }

        public void TextureLoadMetadata(string metadataPath) {
            TextureLoadMetadataJson(System.IO.File.ReadAllText(metadataPath));
        }

        private void TextureLoadMetadataJson(string json) {
            TextureMetadata metadata = JsonUtility.FromJson<TextureMetadata>(json);
            name = metadata.name;
            pyramid_levels = metadata.pyramid_levels;
        }


    }

    [System.Serializable]
    public class TextureFamilyMetadata
    {
        public int[] levelRanges;

        public void TextureFamilyLoadMetadata(TextAsset metadataAsset) {
            TextureFamilyLoadMetadataJson(metadataAsset.text);
        }

        public void TextureFamilyLoadMetadata(string metadataPath) {
            TextureFamilyLoadMetadataJson(System.IO.File.ReadAllText(metadataPath));
        }

        private void TextureFamilyLoadMetadataJson(string json) {
            TextureFamilyMetadata metadata = JsonUtility.FromJson<TextureFamilyMetadata>(json);
            levelRanges = metadata.levelRanges;
        }


    }
}

