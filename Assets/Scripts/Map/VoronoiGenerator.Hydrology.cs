using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// 负责角点水文图、河流选择、流量累计与湿度传播。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        // 在合并后的 Voronoi 角点图上执行洼地填平、下坡选择与流量累计。
        private void BuildDrainage()
        {
            _riverCorners.Clear();
            _riverBoundaryEdges.Clear();
            _riverCells.Clear();
            var cornerLookup = new Dictionary<long, int>();

            for (var cellId = 0; cellId < _cells.Count; cellId++)
            {
                var cell = _cells[cellId];
                var polygon = cell.vertices;
                if (polygon.Count < 2) continue;
                var cornerIds = new int[polygon.Count];
                for (var p = 0; p < polygon.Count; p++)
                {
                    var cornerId = GetOrCreateRiverCorner(polygon[p], cornerLookup);
                    cornerIds[p] = cornerId;
                    AddUnique(_riverCorners[cornerId].AdjacentCells, cellId);
                }

                for (var p = 0; p < polygon.Count; p++)
                {
                    var a = cornerIds[p];
                    var b = cornerIds[(p + 1) % polygon.Count];
                    if (a == b) continue;
                    var edgeKey = EdgeKey(a, b);
                    if (!_riverBoundaryEdges.TryGetValue(edgeKey, out var edge))
                    {
                        edge = new RiverBoundaryEdge(a, b);
                        _riverBoundaryEdges.Add(edgeKey, edge);
                    }
                    AddUnique(edge.AdjacentCells, cellId);
                }
            }

            foreach (var corner in _riverCorners)
            {
                var elevationSum = 0f;
                var landCount = 0;
                foreach (var t in corner.AdjacentCells)
                {
                    var cell = _cells[t];
                    if (cell.isWater) corner.IsOutlet = true;
                    else
                    {
                        elevationSum += cell.elevation;
                        landCount++;
                    }
                }
                corner.Elevation = landCount > 0 ? elevationSum / landCount : 0f;
                corner.FilledElevation = corner.Elevation;
                corner.Flow = landCount > 0 ? 1f : 0f;
                corner.Downslope = -1;
            }

            foreach (var edge in _riverBoundaryEdges.Values)
            {
                var landCount = 0;
                foreach (var t in edge.AdjacentCells)
                    if (!_cells[t].isWater) landCount++;

                // A Civilization-style river occupies an edge shared by two land regions.
                if (landCount < 2) continue;
                AddUnique(_riverCorners[edge.A].Neighbors, edge.B);
                AddUnique(_riverCorners[edge.B].Neighbors, edge.A);
            }

            var visited = new bool[_riverCorners.Count];
            var heap = new MinHeap(_riverCorners.Count);
            for (var i = 0; i < _riverCorners.Count; i++)
            {
                var corner = _riverCorners[i];
                if (!corner.IsOutlet || corner.Neighbors.Count == 0) continue;
                visited[i] = true;
                corner.FilledElevation = 0f;
                heap.Push(i, 0f);
            }

            while (heap.Count > 0)
            {
                var node = heap.Pop();
                var corner = _riverCorners[node.Id];
                foreach (var neighborId in corner.Neighbors)
                {
                    if (visited[neighborId]) continue;
                    visited[neighborId] = true;
                    var neighbor = _riverCorners[neighborId];
                    neighbor.FilledElevation = Mathf.Max(neighbor.Elevation, node.Priority + 0.0001f)
                                               + Hash01(neighborId, variant) * 0.00001f;
                    neighbor.Downslope = node.Id;
                    heap.Push(neighborId, neighbor.FilledElevation);
                }
            }

            var order = new List<int>(_riverCorners.Count);
            for (var i = 0; i < _riverCorners.Count; i++)
                if (visited[i] && _riverCorners[i].Neighbors.Count > 0) order.Add(i);
            order.Sort((a, b) => _riverCorners[b].FilledElevation.CompareTo(_riverCorners[a].FilledElevation));
            foreach (var t in order)
            {
                var corner = _riverCorners[t];
                if (corner.Downslope >= 0) _riverCorners[corner.Downslope].Flow += corner.Flow;
            }
        }

        // 按流量、海拔与间距选择河源，并沿下坡角点追踪河网。
        private void SelectRivers()
        {
            _riverEdges.Clear();
            _riverCells.Clear();
            _selectedRiverSourceCount = 0;
            if (riverCount <= 0) return;
            var candidates = new List<int>();
            for (var i = 0; i < _riverCorners.Count; i++)
            {
                var corner = _riverCorners[i];
                if (!corner.IsOutlet && corner.Elevation > 0.18f && corner.Downslope >= 0) candidates.Add(i);
            }
            candidates.Sort((a, b) =>
                (RiverScore(_riverCorners[b]) + Hash01(b, variant) * 0.18f)
                    .CompareTo(RiverScore(_riverCorners[a]) + Hash01(a, variant) * 0.18f));
            var sources = new List<int>();
            var minimumSpacing = Mathf.Min(size.x, size.y) / Mathf.Sqrt(Mathf.Max(1, riverCount)) * 0.5f;
            for (var i = 0; i < candidates.Count && sources.Count < riverCount; i++)
            {
                var candidate = candidates[i];
                var tooClose = false;
                foreach (var t in sources)
                {
                    if (Vector2.Distance(_riverCorners[candidate].Position, _riverCorners[t].Position) < minimumSpacing)
                    { tooClose = true; break; }
                }
                if (!tooClose) sources.Add(candidate);
            }
            _selectedRiverSourceCount = sources.Count;
            foreach (var t in sources)
            {
                var current = t;
                var safety = _riverCorners.Count;
                while (current >= 0 && !_riverCorners[current].IsOutlet && safety-- > 0)
                {
                    var next = _riverCorners[current].Downslope;
                    if (next < 0 || next == current) break;
                    var edgeKey = EdgeKey(current, next);
                    _riverEdges.Add(edgeKey);
                    if (_riverBoundaryEdges.TryGetValue(edgeKey, out var edge))
                    {
                        foreach (var cellId in edge.AdjacentCells)
                        {
                            if (!_cells[cellId].isWater) _riverCells.Add(cellId);
                        }
                    }
                    current = next;
                }
            }
        }

        private int GetOrCreateRiverCorner(Vector2 position, Dictionary<long, int> lookup)
        {
            var key = CornerKey(position);
            if (lookup.TryGetValue(key, out var existing)) return existing;
            var id = _riverCorners.Count;
            lookup.Add(key, id);
            _riverCorners.Add(new RiverCorner(position));
            return id;
        }

        private static long CornerKey(Vector2 position)
        {
            var x = Mathf.RoundToInt(position.x * 10000f);
            var y = Mathf.RoundToInt(position.y * 10000f);
            return ((long)(uint)x << 32) | (uint)y;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value)) values.Add(value);
        }

        // 以湖泊和河流相邻区域为水源传播湿度，并保持原有排序重分布。
        private void AssignMoisture()
        {
            var count = _cells.Count;
            var distance = new int[count];
            var queue = new Queue<int>();
            for (var i = 0; i < count; i++)
            {
                distance[i] = int.MaxValue;
                if ((_cells[i].isWater && !_cells[i].isOcean) || IsRiverCell(i))
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            }
            var maxDistance = 1;
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var cell = _cells[id];
                foreach (var neighbor in cell.neighbors)
                {
                    if (!_cells[neighbor].isWater && distance[neighbor] == int.MaxValue)
                    {
                        distance[neighbor] = distance[id] + 1;
                        maxDistance = Mathf.Max(maxDistance, distance[neighbor]);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            var land = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (_cells[i].isWater) { _cells[i].moisture = 1f; continue; }
                var normalizedDistance = distance[i] == int.MaxValue ? 1f : distance[i] / (float)maxDistance;
                _cells[i].moisture = 1f - Mathf.Sqrt(Mathf.Clamp01(normalizedDistance));
                land.Add(i);
            }

            land.Sort((a, b) => _cells[a].moisture.CompareTo(_cells[b].moisture));
            for (var i = 0; i < land.Count; i++)
            {
                var t = land.Count <= 1 ? 0.5f : i / (float)(land.Count - 1);
                _cells[land[i]].moisture = Mathf.Lerp(rainfall, 1f + rainfall, t);
            }
        }


        private static float RiverScore(RiverCorner corner) { return corner.Flow * (0.35f + corner.Elevation); }

        private bool IsRiverCell(int id)
        {
            return _riverCells.Contains(id);
        }

        private int CountRiverSources()
        {
            return _selectedRiverSourceCount;
        }


        private sealed class RiverCorner
        {
            public readonly Vector2 Position;
            public readonly List<int> Neighbors = new List<int>();
            public readonly List<int> AdjacentCells = new List<int>();
            public float Elevation;
            public float FilledElevation;
            public float Flow;
            public int Downslope = -1;
            public bool IsOutlet;

            public RiverCorner(Vector2 position)
            {
                Position = position;
            }
        }

        private sealed class RiverBoundaryEdge
        {
            public readonly int A;
            public readonly int B;
            public readonly List<int> AdjacentCells = new List<int>(2);

            public RiverBoundaryEdge(int a, int b)
            {
                A = a;
                B = b;
            }
        }

        private readonly struct HeapNode
        {
            public readonly int Id; public readonly float Priority;
            public HeapNode(int id, float priority) { Id = id; Priority = priority; }
        }

        private sealed class MinHeap
        {
            private readonly List<HeapNode> _nodes;
            public MinHeap(int capacity) { _nodes = new List<HeapNode>(capacity); }
            public int Count => _nodes.Count;
            public void Push(int id, float priority)
            {
                _nodes.Add(new HeapNode(id, priority)); var index = _nodes.Count - 1;
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (_nodes[parent].Priority <= _nodes[index].Priority) break;
                    (_nodes[parent], _nodes[index]) = (_nodes[index], _nodes[parent]);
                    index = parent;
                }
            }
            public HeapNode Pop()
            {
                var result = _nodes[0]; var last = _nodes.Count - 1;
                _nodes[0] = _nodes[last]; _nodes.RemoveAt(last); last--;
                var index = 0;
                while (index <= last)
                {
                    var left = index * 2 + 1; var right = left + 1; if (left > last) break;
                    var smallest = right <= last && _nodes[right].Priority < _nodes[left].Priority ? right : left;
                    if (_nodes[index].Priority <= _nodes[smallest].Priority) break;
                    (_nodes[index], _nodes[smallest]) = (_nodes[smallest], _nodes[index]);
                    index = smallest;
                }
                return result;
            }
        }
    }
}
