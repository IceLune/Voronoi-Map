using System.Collections.Generic;
using DelaunatorSharp;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// 负责站点、Delaunay 邻接和 Voronoi 多边形几何。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        // 根据固定布局参数创建站点；随机状态仍由 Generate 统一管理。
        private List<Vector2> CreateSites()
        {
            var aspect = size.x / size.y;
            var columns = Mathf.Max(2, Mathf.CeilToInt(Mathf.Sqrt(pointCount * aspect)));
            var rows = Mathf.Max(2, Mathf.CeilToInt(pointCount / (float)columns));
            var stepX = size.x / columns;
            var stepY = size.y / rows;
            var sites = new List<Vector2>(pointCount);
            for (var row = 0; row < rows && sites.Count < pointCount; row++)
            {
                for (var column = 0; column < columns && sites.Count < pointCount; column++)
                {
                    var jitterX = UnityEngine.Random.Range(-0.5f, 0.5f) * stepX * pointJitter;
                    var jitterY = UnityEngine.Random.Range(-0.5f, 0.5f) * stepY * pointJitter;
                    sites.Add(new Vector2(-HalfSize.x + (column + 0.5f) * stepX + jitterX,
                        -HalfSize.y + (row + 0.5f) * stepY + jitterY));
                }
            }
            return sites;
        }

        // 构建 Delaunay 邻接关系，并用半平面裁剪得到每个 Voronoi 多边形。
        private void BuildCells(List<Vector2> sites)
        {
            _cells.Clear();
            var points = new IPoint[sites.Count];
            for (var i = 0; i < sites.Count; i++)
            {
                points[i] = new Point(sites[i].x, sites[i].y);
                _cells.Add(new VoronoiCell { id = i, site = sites[i] });
            }

            var delaunay = new Delaunator(points);
            var triangles = delaunay.Triangles;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                AddNeighborPair(triangles[i], triangles[i + 1]);
                AddNeighborPair(triangles[i + 1], triangles[i + 2]);
                AddNeighborPair(triangles[i + 2], triangles[i]);
            }

            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                var polygon = new List<Vector2>(4)
                {
                    new Vector2(-HalfSize.x, -HalfSize.y), new Vector2(HalfSize.x, -HalfSize.y),
                    new Vector2(HalfSize.x, HalfSize.y), new Vector2(-HalfSize.x, HalfSize.y)
                };
                for (var n = 0; n < cell.neighbors.Count && polygon.Count > 0; n++)
                {
                    ClipToSiteHalfPlane(polygon, cell.site, _cells[cell.neighbors[n]].site);
                }
                cell.vertices = polygon;
            }
        }

        private void AddNeighborPair(int a, int b)
        {
            if (a == b) return;
            if (!_cells[a].neighbors.Contains(b)) _cells[a].neighbors.Add(b);
            if (!_cells[b].neighbors.Contains(a)) _cells[b].neighbors.Add(a);
        }

        // 将多边形裁剪到当前站点相对邻居的 Voronoi 半平面内。
        private static void ClipToSiteHalfPlane(List<Vector2> polygon, Vector2 site, Vector2 other)
        {
            if (polygon.Count == 0) return;
            var normal = other - site;
            var limit = (other.sqrMagnitude - site.sqrMagnitude) * 0.5f;
            var output = new List<Vector2>(polygon.Count + 1);
            var previous = polygon[^1];
            var previousDistance = Vector2.Dot(previous, normal) - limit;
            foreach (var current in polygon)
            {
                var currentDistance = Vector2.Dot(current, normal) - limit;
                var currentInside = currentDistance <= 0.0001f;
                var previousInside = previousDistance <= 0.0001f;
                if (currentInside != previousInside)
                {
                    var denominator = previousDistance - currentDistance;
                    var t = Mathf.Abs(denominator) < 0.000001f ? 0f : previousDistance / denominator;
                    output.Add(Vector2.Lerp(previous, current, Mathf.Clamp01(t)));
                }
                if (currentInside) output.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }
            polygon.Clear();
            polygon.AddRange(output);
        }


        private static Vector2 PolygonCentroid(List<Vector2> vertices, Vector2 fallback)
        {
            if (vertices == null || vertices.Count < 3) return fallback;
            var signedArea = 0f; var centroid = Vector2.zero;
            for (var i = 0; i < vertices.Count; i++)
            {
                var a = vertices[i]; var b = vertices[(i + 1) % vertices.Count];
                var cross = a.x * b.y - b.x * a.y; signedArea += cross; centroid += (a + b) * cross;
            }
            return Mathf.Abs(signedArea) < 0.000001f ? fallback : centroid / (3f * signedArea);
        }

    }
}
