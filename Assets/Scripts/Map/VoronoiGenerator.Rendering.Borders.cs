using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Map
{
    /// <summary>
    /// 负责海岸线、湖岸线和可选区域边界的判定与网格生成。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        /// <summary>生成去重后的边界带状网格。</summary>
        private void BuildBorderMesh()
        {
            EnsureBorderObject();
            DestroyGeneratedObject(ref _borderMesh);
            _borderMesh = new Mesh { name = "Generated Voronoi Borders", hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                var polygon = cell.vertices;
                for (var p = 0; p < polygon.Count; p++)
                {
                    var a = polygon[p];
                    var b = polygon[(p + 1) % polygon.Count];
                    var neighbor = FindNeighborAcrossEdge(cell, a, b);
                    if (neighbor != null && cell.id > neighbor.id) continue;
                    if (!TryGetEdgeColor(cell, neighbor, out var borderColor)) continue;

                    var shoreline = IsShoreline(cell, neighbor);
                    // 填充多边形使用精确的 Voronoi 边。海岸接缝也必须贴合该边，
                    // 避免噪声装饰偏离后露出大陆与水域之间的裂缝。
                    var line = shoreline
                        ? new List<Vector2> { a, b }
                        : GetEdgePoints(a, b, 0.13f);
                    var halfWidth = shoreline ? 0.018f : 0.006f;
                    for (var j = 0; j < line.Count - 1; j++)
                    {
                        AddRibbonSegment(vertices, colors, triangles, line[j], line[j + 1], halfWidth, borderColor, -0.04f);
                    }
                }
            }

            if (vertices.Count > 65535) _borderMesh.indexFormat = IndexFormat.UInt32;
            _borderMesh.SetVertices(vertices);
            _borderMesh.SetColors(colors);
            _borderMesh.SetTriangles(triangles, 0);
            _borderMesh.RecalculateBounds();
            _borderObject.GetComponent<MeshFilter>().sharedMesh = _borderMesh;
            _borderObject.SetActive(displayMode == MapDisplayMode.FullMap);
        }

        /// <summary>从当前区域的邻接表中找出共享指定 Voronoi 边的区域。</summary>
        private VoronoiCell FindNeighborAcrossEdge(VoronoiCell cell, Vector2 a, Vector2 b)
        {
            var midpoint = (a + b) * 0.5f;
            var ownDistance = (cell.site - midpoint).sqrMagnitude;
            var bestError = float.MaxValue;
            VoronoiCell best = null;
            foreach (var t in cell.neighbors)
            {
                var candidate = _cells[t];
                var error = Mathf.Abs((candidate.site - midpoint).sqrMagnitude - ownDistance);
                if (error < bestError)
                {
                    bestError = error;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>判断公共边是否是海岸线或湖岸线。</summary>
        private static bool IsShoreline(VoronoiCell cell, VoronoiCell neighbor)
        {
            if (neighbor == null) return false;
            if (cell.isOcean != neighbor.isOcean) return true;
            return cell.isWater != neighbor.isWater && !cell.isOcean && !neighbor.isOcean;
        }

        /// <summary>根据公共边两侧区域的类型和显示设置选择边界颜色。</summary>
        private bool TryGetEdgeColor(VoronoiCell cell, VoronoiCell neighbor, out Color color)
        {
            color = Color.clear;
            if (neighbor == null)
            {
                if (!showRegionBorders) return false;
                var edgeBase = biomes ? BiomeColor(cell) : SmoothColor(cell);
                color = new Color(edgeBase.r * 0.78f, edgeBase.g * 0.78f, edgeBase.b * 0.78f, 0.45f);
                return true;
            }

            if (cell.isOcean != neighbor.isOcean)
            {
                color = Rgb(0x33, 0x33, 0x5a);
                return true;
            }

            if (cell.isWater != neighbor.isWater && !cell.isOcean && !neighbor.isOcean)
            {
                var waterCell = cell.isWater ? cell : neighbor;
                var landCell = cell.isWater ? neighbor : cell;
                if (waterCell.biome == MapBiome.Ice && IsFrozenLandBiome(landCell.biome))
                {
                    if (!showRegionBorders) return false;
                    color = new Color(0.58f, 0.66f, 0.70f, 0.42f);
                    return true;
                }

                color = Rgb(0x22, 0x55, 0x88);
                return true;
            }

            if (!showRegionBorders) return false;
            var cellColor = biomes ? BiomeColor(cell) : SmoothColor(cell);
            var neighborColor = biomes ? BiomeColor(neighbor) : SmoothColor(neighbor);
            var edgeColor = Color.Lerp(cellColor, neighborColor, 0.5f);
            color = new Color(edgeColor.r * 0.78f, edgeColor.g * 0.78f, edgeColor.b * 0.78f, 0.45f);
            return true;
        }

        /// <summary>判断陆地区域是否属于会与冰面相接的寒冷生物群系。</summary>
        private static bool IsFrozenLandBiome(MapBiome biome)
        {
            return biome is MapBiome.Snow or MapBiome.Tundra or MapBiome.Bare or MapBiome.Scorched;
        }

        /// <summary>确保边界子对象及其网格渲染组件存在。</summary>
        private void EnsureBorderObject()
        {
            if (!_borderObject)
            {
                var existing = transform.Find("Generated Borders");
                _borderObject = existing ? existing.gameObject : new GameObject("Generated Borders");
                _borderObject.transform.SetParent(transform, false);
            }

            var filter = _borderObject.GetComponent<MeshFilter>();
            if (!filter) _borderObject.AddComponent<MeshFilter>();
            var meshRenderer = _borderObject.GetComponent<MeshRenderer>();
            if (!meshRenderer) meshRenderer = _borderObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetGeneratedMaterial();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = 1;
        }

        /// <summary>返回稳定、可复现的直线或噪声边界采样点。</summary>
        private List<Vector2> GetEdgePoints(Vector2 start, Vector2 end, float amplitude)
        {
            var points = new List<Vector2>();
            if (!noisyEdges)
            {
                points.Add(start);
                points.Add(end);
                return points;
            }

            var reverse = start.x > end.x || (Mathf.Approximately(start.x, end.x) && start.y > end.y);
            var a = reverse ? end : start;
            var b = reverse ? start : end;
            var stableSalt = HashVector(a) ^ (HashVector(b) * 397) ^ 12345;
            AppendNoisyEdge(points, a, b, 0, stableSalt, amplitude);
            if (reverse) points.Reverse();
            return points;
        }

        /// <summary>递归细分边段，为普通区域边界添加稳定噪声。</summary>
        private static void AppendNoisyEdge(List<Vector2> output, Vector2 a, Vector2 b, int depth, int salt, float amplitude)
        {
            while (true)
            {
                if (depth == 0) output.Add(a);
                if (depth >= 2 || (b - a).sqrMagnitude < 0.025f)
                {
                    output.Add(b);
                    return;
                }

                var direction = b - a;
                var perpendicular = new Vector2(-direction.y, direction.x).normalized;
                var division = Mathf.Lerp(0.36f, 0.64f, Hash01(salt, depth + 17));
                var midpoint = Vector2.Lerp(a, b, division) + perpendicular * (direction.magnitude * (Hash01(salt, depth + 101) - 0.5f) * amplitude);
                AppendNoisyEdge(output, a, midpoint, depth + 1, salt * 31 + 7, amplitude * 0.7f);
                a = midpoint;
                depth += 1;
                salt = salt * 31 + 13;
                amplitude *= 0.7f;
            }
        }
    }
}
