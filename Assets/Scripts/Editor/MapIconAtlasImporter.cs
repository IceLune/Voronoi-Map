using System.Collections.Generic;
using System.IO;
using Map;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 自动导入和重建图标图集
    /// </summary>
    public sealed class MapIconAtlasImporter : AssetPostprocessor
    {
        private const string AtlasPath = "Assets/Resources/map-icons.png";
        private const string LibraryPath = "Assets/Resources/MapIconLibrary.asset";
        private const int SpriteSize = 100;
        private const int Left = 9;
        private const int Top = 4;
        private static readonly string[] RowNames =
        {
            "water", "mountains", "shrub", "desert", "jungle",
            "forest", "grassland", "marsh", "winter_forest", "northern_forest"
        };

        private void OnPreprocessTexture()
        {
            if (assetPath != AtlasPath) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = SpriteSize;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existingRects = provider.GetSpriteRects();
            var existingIds = new Dictionary<string, GUID>();
            foreach (var t in existingRects)
                existingIds[t.name] = t.spriteID;

            importer.GetSourceTextureWidthAndHeight(out _, out var textureHeight);
            var rects = new List<SpriteRect>(RowNames.Length * 5);
            for (var row = 0; row < RowNames.Length; row++)
            {
                for (var variant = 0; variant < 5; variant++)
                {
                    var column = variant + 1;
                    var spriteName = RowNames[row] + "_" + (variant + 1).ToString("00");
                    var sourceTop = Top + row * SpriteSize;
                    var rectHeight = Mathf.Min(SpriteSize, textureHeight - sourceTop);
                    rects.Add(new SpriteRect
                    {
                        name = spriteName,
                        rect = new Rect(Left + column * SpriteSize,
                            textureHeight - sourceTop - rectHeight,
                            SpriteSize, rectHeight),
                        alignment = SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                        spriteID = existingIds.TryGetValue(spriteName, out var existingId) ? existingId : GUID.Generate()
                    });
                }
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.Apply();
        }

        [InitializeOnLoadMethod]
        private static void ScheduleInitialBuild()
        {
            EditorApplication.delayCall += EnsureAtlasAndLibrary;
        }

        private static void EnsureAtlasAndLibrary()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);
            var spriteCount = 0;
            foreach (var t in assets)
                if (t is Sprite) spriteCount++;

            if (spriteCount != RowNames.Length * 5)
            {
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
                return;
            }
            RebuildLibrary();
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            var atlasImported = false;
            for (var i = 0; i < importedAssets.Length; i++)
                if (importedAssets[i] == AtlasPath) { atlasImported = true; break; }
            if (!atlasImported) return;
            EditorApplication.delayCall += RebuildLibrary;
        }

        [MenuItem("Tools/Mapgen2/Rebuild Icon Sprites")]
        public static void RebuildLibrary()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);
            var sprites = new Dictionary<string, Sprite>();
            foreach (var t in assets)
                if (t is Sprite sprite) sprites[sprite.name] = sprite;

            var library = AssetDatabase.LoadAssetAtPath<MapIconLibrary>(LibraryPath);
            if (!library)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath) ?? "Assets/Resources");
                library = ScriptableObject.CreateInstance<MapIconLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.water.sprites = GetVariants(sprites, "water");
            library.mountains.sprites = GetVariants(sprites, "mountains");
            library.shrub.sprites = GetVariants(sprites, "shrub");
            library.desert.sprites = GetVariants(sprites, "desert");
            library.jungle.sprites = GetVariants(sprites, "jungle");
            library.forest.sprites = GetVariants(sprites, "forest");
            library.grassland.sprites = GetVariants(sprites, "grassland");
            library.marsh.sprites = GetVariants(sprites, "marsh");
            library.winterForest.sprites = GetVariants(sprites, "winter_forest");
            library.northernForest.sprites = GetVariants(sprites, "northern_forest");
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            if (sprites.Count == RowNames.Length * 5)
                Debug.Log("Mapgen2 icon atlas rebuilt: 50 named Sprite assets and MapIconLibrary updated.");
            else
                Debug.LogWarning("Mapgen2 icon atlas contains " + sprites.Count + " of 50 expected Sprite assets.");
        }

        private static Sprite[] GetVariants(Dictionary<string, Sprite> sprites, string prefix)
        {
            var result = new Sprite[5];
            for (var i = 0; i < result.Length; i++) sprites.TryGetValue(prefix + "_" + (i + 1).ToString("00"), out result[i]);
            return result;
        }
    }
}
