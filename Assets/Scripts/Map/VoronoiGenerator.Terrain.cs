using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// 负责岛屿、水域、海拔、温度和生物群系计算。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        // 按照原有岛屿噪声、边界强制海洋与洪水填充规则划分陆地和水域。
        private void AssignTerrain()
        {
            var noiseOffset = SeedOffset(seed);
            var oceanQueue = new Queue<int>();
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                var nx = cell.site.x / HalfSize.x;
                var ny = cell.site.y / HalfSize.y;
                var distance = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                var signedNoise = FractalNoise(cell.site, noiseOffset) * 2f - 1f;
                var roundedNoise = Mathf.Lerp(signedNoise, 0.5f, islandRoundness);
                var landSignal = roundedNoise - (1f - islandIrregularity) * distance * distance;
                var seaBias = (seaLevel - 0.5f) * 0.8f;
                cell.isWater = landSignal < seaBias || TouchesBoundary(cell.vertices);
                cell.elevation = cell.isWater ? -0.1f : 0f;
                cell.isOcean = false;
                cell.isCoast = false;
                cell.downslope = -1;
                cell.flow = 0f;
                if (!cell.isWater || !TouchesBoundary(cell.vertices)) continue;
                cell.isOcean = true;
                oceanQueue.Enqueue(i);
            }

            while (oceanQueue.Count > 0)
            {
                var ocean = _cells[oceanQueue.Dequeue()];
                foreach (var t in ocean.neighbors)
                {
                    var neighbor = _cells[t];
                    if (neighbor.isWater && !neighbor.isOcean)
                    {
                        neighbor.isOcean = true;
                        oceanQueue.Enqueue(neighbor.id);
                    }
                }
            }
            foreach (var cell in _cells)
            {
                if (cell.isWater) continue;
                foreach (var t in cell.neighbors)
                {
                    if (_cells[t].isOcean) { cell.isCoast = true; break; }
                }
            }
            AssignElevationFromCoast();
        }

        // 从海岸向内传播距离，再按原曲线重新分布陆地海拔。
        private void AssignElevationFromCoast()
        {
            var count = _cells.Count;
            var distance = new int[count];
            var queue = new Queue<int>();
            for (var i = 0; i < count; i++)
            {
                distance[i] = int.MaxValue;
                if (_cells[i].isCoast)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in _cells[current].neighbors)
                {
                    if (_cells[neighbor].isOcean || distance[neighbor] != int.MaxValue) continue;
                    distance[neighbor] = distance[current] + (_cells[neighbor].isWater ? 0 : 1);
                    queue.Enqueue(neighbor);
                }
            }

            var land = new List<int>();
            for (var i = 0; i < count; i++)
            {
                if (!_cells[i].isWater) land.Add(i);
                else _cells[i].elevation = _cells[i].isOcean ? -0.1f : 0f;
            }
            land.Sort((a, b) => distance[a].CompareTo(distance[b]));
            const float scaleFactor = 1.1f;
            for (var i = 0; i < land.Count; i++)
            {
                var y = land.Count <= 1 ? 0f : i / (float)(land.Count - 1);
                _cells[land[i]].elevation = Mathf.Min(1f,
                    Mathf.Sqrt(scaleFactor) - Mathf.Sqrt(scaleFactor * (1f - y)));
            }
        }


        // 结合纬度、海拔和湿度计算温度与生物群系。
        private void AssignBiomes()
        {
            foreach (var t in _cells)
            {
                var latitude = Mathf.InverseLerp(HalfSize.y, -HalfSize.y, t.site.y);
                var temperatureBias = Mathf.Lerp(northTemperature, southTemperature, latitude);
                t.temperature = 1f - t.elevation + temperatureBias;
                t.biome = ChooseBiome(t);
            }
        }


        // 生成与旧实现一致的五层分形噪声，不改变振幅和频率顺序。
        private float FractalNoise(Vector2 position, Vector2 offset)
        {
            var amplitude = 1f;
            var frequency = noiseScale;
            var total = 0f;
            var weight = 0f;
            var octavePersistence = Mathf.Pow(0.5f, 1f + persistence);
            for (var octave = 0; octave < 5; octave++)
            {
                total += Mathf.PerlinNoise(offset.x + position.x * frequency, offset.y + position.y * frequency) * amplitude;
                weight += amplitude;
                amplitude *= octavePersistence;
                frequency *= 2f;
            }
            return total / weight;
        }

        private static Vector2 SeedOffset(int value)
        {
            unchecked
            {
                var hash = (uint)value;
                hash ^= hash >> 16; hash *= 0x7feb352d; hash ^= hash >> 15; hash *= 0x846ca68b; hash ^= hash >> 16;
                return new Vector2((hash & 0xffff) / 65535f * 4096f, ((hash >> 16) & 0xffff) / 65535f * 4096f);
            }
        }


        private static MapBiome ChooseBiome(VoronoiCell cell)
        {
            if (cell.isOcean) return MapBiome.Ocean;
            if (cell.isWater)
            {
                return cell.temperature switch
                {
                    > 0.9f => MapBiome.Marsh,
                    < 0.2f => MapBiome.Ice,
                    _ => MapBiome.Lake
                };
            }
            if (cell.isCoast) return MapBiome.Beach;
            var t = cell.temperature; var m = cell.moisture;
            return t switch
            {
                < 0.2f when m > 0.5f => MapBiome.Snow,
                < 0.2f when m > 0.33f => MapBiome.Tundra,
                < 0.2f when m > 0.16f => MapBiome.Bare,
                < 0.2f => MapBiome.Scorched,
                < 0.4f when m > 0.66f => MapBiome.Taiga,
                < 0.4f when m > 0.33f => MapBiome.Shrubland,
                < 0.4f => MapBiome.TemperateDesert,
                < 0.7f when m > 0.83f => MapBiome.TemperateRainForest,
                < 0.7f when m > 0.5f => MapBiome.TemperateDeciduousForest,
                < 0.7f when m > 0.16f => MapBiome.Grassland,
                < 0.7f => MapBiome.TemperateDesert,
                _ => m switch
                {
                    > 0.66f => MapBiome.TropicalRainForest,
                    > 0.33f => MapBiome.TropicalSeasonalForest,
                    > 0.16f => MapBiome.Grassland,
                    _ => MapBiome.SubtropicalDesert
                }
            };
        }

        private static Color BiomeColor(VoronoiCell cell)
        {
            var color = cell.biome switch
            {
                MapBiome.Ocean => Rgb(0x44, 0x44, 0x7a),
                MapBiome.Lake => Rgb(0x33, 0x66, 0x99),
                MapBiome.Marsh => Rgb(0x2f, 0x66, 0x66),
                MapBiome.Ice => Rgb(0x99, 0xff, 0xff),
                MapBiome.Beach => Rgb(0xa0, 0x90, 0x77),
                MapBiome.Snow => Color.white,
                MapBiome.Tundra => Rgb(0xbb, 0xbb, 0xaa),
                MapBiome.Bare => Rgb(0x88, 0x88, 0x88),
                MapBiome.Scorched => Rgb(0x55, 0x55, 0x55),
                MapBiome.Taiga => Rgb(0x99, 0xaa, 0x77),
                MapBiome.Shrubland => Rgb(0x88, 0x99, 0x77),
                MapBiome.TemperateDesert => Rgb(0xc9, 0xd2, 0x9b),
                MapBiome.TemperateRainForest => Rgb(0x44, 0x88, 0x55),
                MapBiome.TemperateDeciduousForest => Rgb(0x67, 0x94, 0x59),
                MapBiome.Grassland => Rgb(0x88, 0xaa, 0x55),
                MapBiome.TropicalRainForest => Rgb(0x33, 0x77, 0x55),
                MapBiome.TropicalSeasonalForest => Rgb(0x55, 0x99, 0x44),
                _ => Rgb(0xd2, 0xb9, 0x8b)
            };
            return color;
        }

        private static Color SmoothColor(VoronoiCell cell)
        {
            if (cell.isWater && !cell.isOcean) return BiomeColor(cell);
            if (cell.elevation < 0f)
            {
                return new Color(
                    Mathf.Clamp01((48f + 48f * cell.elevation) / 255f),
                    Mathf.Clamp01((64f + 64f * cell.elevation) / 255f),
                    Mathf.Clamp01((127f + 128f * cell.elevation) / 255f));
            }

            var temperature = Mathf.Clamp01(cell.temperature);
            var moisture = Mathf.Clamp01(cell.moisture);
            var white = (1f - temperature) * (1f - temperature);
            moisture = 1f - (1f - moisture) * (1f - moisture);
            var red = 210f - 100f * moisture;
            var green = 185f - 45f * moisture;
            var blue = 139f - 45f * moisture;
            return new Color(
                Mathf.Lerp(red / 255f, 1f, white),
                Mathf.Lerp(green / 255f, 1f, white),
                Mathf.Lerp(blue / 255f, 1f, white));
        }


        private bool TouchesBoundary(List<Vector2> vertices)
        {
            const float tolerance = 0.002f;
            foreach (var point in vertices)
            {
                if (Mathf.Abs(Mathf.Abs(point.x) - HalfSize.x) < tolerance || Mathf.Abs(Mathf.Abs(point.y) - HalfSize.y) < tolerance) return true;
            }
            return false;
        }

    }
}
