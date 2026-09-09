using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Map
{
    /// <summary>
    /// 负责地图地表网格、显示模式颜色和地形明暗计算。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        /// <summary>根据每个 Voronoi 多边形生成带顶点色的地表三角形网格。</summary>
        private void BuildMapMesh()
        {
            EnsureRenderComponents();
            DestroyGeneratedObject(ref _mapMesh);
            _mapMesh = new Mesh { name = "Generated Voronoi Map", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var vertexCount = 0;
            foreach (var t in _cells)
                vertexCount += Mathf.Max(0, t.vertices.Count - 2) * 3;

            if (vertexCount > 65535) _mapMesh.indexFormat = IndexFormat.UInt32;
            var vertices = new List<Vector3>(vertexCount);
            var colors = new List<Color>(vertexCount);
            var triangles = new List<int>(vertexCount);
            foreach (var cell in _cells)
            {
                if (cell.vertices.Count < 3) continue;
                var color = DisplayColor(cell);
                var anchor = cell.vertices[0];
                for (var p = 1; p < cell.vertices.Count - 1; p++)
                {
                    AddMapVertex(vertices, colors, triangles, anchor, color);
                    AddMapVertex(vertices, colors, triangles, cell.vertices[p], color);
                    AddMapVertex(vertices, colors, triangles, cell.vertices[p + 1], color);
                }
            }
            _mapMesh.SetVertices(vertices);
            _mapMesh.SetColors(colors);
            _mapMesh.SetTriangles(triangles, 0);
            _mapMesh.RecalculateBounds();
            _mapMesh.RecalculateNormals();
            GetComponent<MeshFilter>().sharedMesh = _mapMesh;
        }

        /// <summary>确保地图根对象具备地表网格所需的 Unity 渲染组件。</summary>
        private void EnsureRenderComponents()
        {
            var filter = GetComponent<MeshFilter>();
            if (!filter) gameObject.AddComponent<MeshFilter>();
            var meshRenderer = GetComponent<MeshRenderer>();
            if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetGeneratedMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = 0;
        }

        /// <summary>按当前显示模式计算区域的最终顶点色。</summary>
        private Color DisplayColor(VoronoiCell cell)
        {
            switch (displayMode)
            {
                case MapDisplayMode.Elevation:
                    if (cell.isOcean) return new Color(0.06f, 0.19f, 0.36f);
                    if (cell.isWater) return new Color(0.19f, 0.42f, 0.62f);
                    return Color.Lerp(new Color(0.18f, 0.25f, 0.18f), new Color(0.96f, 0.93f, 0.78f), cell.elevation);
                case MapDisplayMode.Moisture:
                    return Color.Lerp(new Color(0.75f, 0.63f, 0.34f), new Color(0.08f, 0.36f, 0.62f), cell.moisture);
                case MapDisplayMode.Rivers:
                    return IsRiverCell(cell.id) ? new Color(0.11f, 0.56f, 0.90f) : (cell.isWater ? new Color(0.07f, 0.18f, 0.29f) : new Color(0.77f, 0.72f, 0.57f));
                case MapDisplayMode.Voronoi:
                    return new Color(0.82f, 0.85f, 0.80f);
                default:
                    var color = biomes ? BiomeColor(cell) : SmoothColor(cell);
                    if (noisyFills)
                    {
                        var grain = Mathf.Lerp(0.94f, 1.06f, Hash01(cell.id, 12345));
                        color = new Color(color.r * grain, color.g * grain, color.b * grain, color.a);
                    }
                    if (lighting && !cell.isWater)
                    {
                        var shade = ReliefShade(cell);
                        color = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                    }
                    return color;
            }
        }

        /// <summary>根据相邻区域的海拔梯度计算简易浮雕光照系数。</summary>
        private float ReliefShade(VoronoiCell cell)
        {
            var gradient = Vector2.zero;
            foreach (var t in cell.neighbors)
            {
                var neighbor = _cells[t];
                var delta = neighbor.site - cell.site;
                var lengthSquared = Mathf.Max(0.0001f, delta.sqrMagnitude);
                gradient += delta * ((neighbor.elevation - cell.elevation) / lengthSquared);
            }
            var dot = Vector2.Dot(gradient, new Vector2(-0.7071f, 0.7071f));
            return Mathf.Clamp(0.88f + dot * 2.2f + cell.elevation * 0.08f, 0.62f, 1.18f);
        }

        /// <summary>向地表网格同时追加顶点、颜色和顺序索引。</summary>
        private static void AddMapVertex(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector2 point, Color color)
        {
            triangles.Add(vertices.Count); vertices.Add(new Vector3(point.x, point.y, 0f)); colors.Add(color);
        }
    }
}
