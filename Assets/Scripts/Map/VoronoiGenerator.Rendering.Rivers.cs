using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Map
{
    /// <summary>
    /// 负责将水文角点图中的河流边转换为沿 Voronoi 公共边流动的网格。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        /// <summary>按照流量计算河宽，并生成河岸、河面与连接圆盘。</summary>
        private void BuildRiverMesh()
        {
            EnsureRiverObject();
            DestroyGeneratedObject(ref _riverMesh);
            _riverMesh = new Mesh { name = "Generated Rivers", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var vertices = new List<Vector3>(_riverEdges.Count * 64);
            var colors = new List<Color>(_riverEdges.Count * 64);
            var triangles = new List<int>(_riverEdges.Count * 96);
            var bankColor = new Color(0.12f, 0.35f, 0.52f, 1f);
            var riverColor = new Color(0.20f, 0.64f, 0.86f, 1f);
            var degrees = new int[_riverCorners.Count];
            foreach (var key in _riverEdges)
            {
                DecodeEdgeKey(key, out var degreeA, out var degreeB);
                degrees[degreeA]++;
                degrees[degreeB]++;
            }
            foreach (var key in _riverEdges)
            {
                DecodeEdgeKey(key, out var a, out var b);
                var cornerA = _riverCorners[a];
                var cornerB = _riverCorners[b];
                var start = cornerA.Position;
                var end = cornerB.Position;
                var reachesWater = cornerA.IsOutlet || cornerB.IsOutlet;
                var flow = Mathf.Max(cornerA.Flow, cornerB.Flow);
                var width = riverWidth * Mathf.Lerp(0.75f, 1.65f, Mathf.Clamp01(Mathf.Log(flow + 1f, 10f)));
                AddRibbonSegment(vertices, colors, triangles, start, end, width * 1.24f, bankColor, -0.075f);
                AddRibbonSegment(vertices, colors, triangles, start, end, width, riverColor, -0.08f);
                var startWidth = reachesWater && cornerA.IsOutlet ? width * 1.25f : width;
                var endWidth = reachesWater && cornerB.IsOutlet ? width * 1.25f : width;
                if (degrees[a] != 2 || cornerA.IsOutlet)
                    AddDisc(vertices, colors, triangles, start, startWidth * 1.24f, bankColor, -0.075f);
                AddDisc(vertices, colors, triangles, start, startWidth * 0.92f, riverColor, -0.08f);
                if (degrees[b] != 2 || cornerB.IsOutlet)
                    AddDisc(vertices, colors, triangles, end, endWidth * 1.24f, bankColor, -0.075f);
                AddDisc(vertices, colors, triangles, end, endWidth * 0.92f, riverColor, -0.08f);
            }
            if (vertices.Count > 65535) _riverMesh.indexFormat = IndexFormat.UInt32;
            _riverMesh.SetVertices(vertices);
            _riverMesh.SetColors(colors);
            _riverMesh.SetTriangles(triangles, 0);
            _riverMesh.RecalculateBounds();
            _riverObject.GetComponent<MeshFilter>().sharedMesh = _riverMesh;
            _riverObject.SetActive(_riverEdges.Count > 0 && (displayMode == MapDisplayMode.FullMap || displayMode == MapDisplayMode.Rivers));
        }

        /// <summary>确保河流子对象及其网格渲染组件存在。</summary>
        private void EnsureRiverObject()
        {
            if (!_riverObject)
            {
                var existing = transform.Find("Generated Rivers");
                _riverObject = existing ? existing.gameObject : new GameObject("Generated Rivers");
                _riverObject.transform.SetParent(transform, false);
            }
            var filter = _riverObject.GetComponent<MeshFilter>();
            if (!filter) _riverObject.AddComponent<MeshFilter>();
            var meshRenderer = _riverObject.GetComponent<MeshRenderer>();
            if (!meshRenderer) meshRenderer = _riverObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetGeneratedMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = 2;
        }

        /// <summary>在河段端点或分叉处追加圆盘，遮盖带状网格之间的缝隙。</summary>
        private static void AddDisc(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Vector2 center, float radius, Color color, float z)
        {
            const int segmentCount = 12;
            var first = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, z));
            colors.Add(color);
            for (var i = 0; i <= segmentCount; i++)
            {
                var angle = i / (float)segmentCount * Mathf.PI * 2f;
                vertices.Add(new Vector3(center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius, z));
                colors.Add(color);
            }
            for (var i = 0; i < segmentCount; i++)
            {
                triangles.Add(first);
                triangles.Add(first + i + 1);
                triangles.Add(first + i + 2);
            }
        }
    }
}
