using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Map
{
    /// <summary>
    /// 负责从图标配置资源选择独立 Sprite，并将可见图标合批为单个网格。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        /// <summary>按区域属性选择图标，并生成使用 Sprite 原始 UV 的四边形。</summary>
        private void BuildIconMesh()
        {
            EnsureIconObject();
            DestroyGeneratedObject(ref _iconMesh);
            var library = iconLibrary ? iconLibrary : Resources.Load<MapIconLibrary>("MapIconLibrary");
            if (!icons || displayMode != MapDisplayMode.FullMap || !library)
            {
                _iconObject.SetActive(false);
                return;
            }

            _iconMesh = new Mesh { name = "Generated Map Icons", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Texture2D atlas = null;
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                var sprite = library.GetSprite(cell, Hash01(i, variant), HalfSize.y * 0.4f);
                if (!sprite || cell.vertices.Count < 3) continue;
                if (!atlas) atlas = sprite.texture;
                if (sprite.texture != atlas) continue;
                var radius = Mathf.Clamp(NearestNeighborDistance(cell) * 0.31f, 0.055f, 0.23f);
                var textureRect = sprite.textureRect;
                var u0 = textureRect.xMin / atlas.width;
                var u1 = textureRect.xMax / atlas.width;
                var v0 = textureRect.yMin / atlas.height;
                var v1 = textureRect.yMax / atlas.height;
                var first = vertices.Count;
                vertices.Add(new Vector3(cell.site.x - radius, cell.site.y - radius, -0.12f));
                vertices.Add(new Vector3(cell.site.x + radius, cell.site.y - radius, -0.12f));
                vertices.Add(new Vector3(cell.site.x + radius, cell.site.y + radius, -0.12f));
                vertices.Add(new Vector3(cell.site.x - radius, cell.site.y + radius, -0.12f));
                uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
                for (var c = 0; c < 4; c++) colors.Add(Color.white);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }

            if (vertices.Count > 65535) _iconMesh.indexFormat = IndexFormat.UInt32;
            _iconMesh.SetVertices(vertices);
            _iconMesh.SetColors(colors);
            _iconMesh.SetUVs(0, uv);
            _iconMesh.SetTriangles(triangles, 0);
            _iconMesh.RecalculateBounds();
            _iconObject.GetComponent<MeshFilter>().sharedMesh = _iconMesh;
            if (atlas) _iconObject.GetComponent<MeshRenderer>().sharedMaterial = GetIconMaterial(atlas);
            _iconObject.SetActive(vertices.Count > 0 && atlas);
        }

        /// <summary>确保图标子对象及其网格渲染组件存在。</summary>
        private void EnsureIconObject()
        {
            if (!_iconObject)
            {
                var existing = transform.Find("Generated Icons");
                _iconObject = existing ? existing.gameObject : new GameObject("Generated Icons");
                _iconObject.transform.SetParent(transform, false);
            }
            if (!_iconObject.GetComponent<MeshFilter>()) _iconObject.AddComponent<MeshFilter>();
            var meshRenderer = _iconObject.GetComponent<MeshRenderer>();
            if (!meshRenderer) meshRenderer = _iconObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = 3;
        }

        /// <summary>获取使用图集纹理的 Sprite 材质，并在首次使用时创建。</summary>
        private Material GetIconMaterial(Texture2D atlas)
        {
            if (!_iconMaterial)
            {
                var shader = Shader.Find("Sprites/Default");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                _iconMaterial = new Material(shader)
                {
                    name = "Generated Map Icon Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            _iconMaterial.mainTexture = atlas;
            return _iconMaterial;
        }

        /// <summary>计算区域站点到最近邻接站点的距离，用于限制图标尺寸。</summary>
        private float NearestNeighborDistance(VoronoiCell cell)
        {
            var distance = float.MaxValue;
            foreach (var t in cell.neighbors)
            {
                distance = Mathf.Min(distance, Vector2.Distance(cell.site, _cells[t].site));
            }
            return Mathf.Approximately(distance, float.MaxValue) ? 0.2f : distance;
        }
    }
}
