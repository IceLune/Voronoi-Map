using System;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// 地图生物群系到独立 Sprite 变体的配置资源。
    /// 资源只负责选择图标，不参与地图地形和水文生成。
    /// </summary>
    [CreateAssetMenu(fileName = "MapIconLibrary", menuName = "Mapgen2/Icon Library")]
    public sealed class MapIconLibrary : ScriptableObject
    {
        /// <summary>同一图标类别下可稳定随机选择的一组 Sprite。</summary>
        [Serializable]
        public sealed class SpriteVariants
        {
            [Tooltip("该类别可选的独立 Sprite。选择值相同时会返回相同下标。")]
            public Sprite[] sprites = Array.Empty<Sprite>();

            /// <summary>用 0～1 的选择值稳定映射到一个 Sprite。</summary>
            public Sprite Get(float selection)
            {
                if (sprites == null || sprites.Length == 0) return null;
                var index = Mathf.Clamp(Mathf.FloorToInt(selection * sprites.Length), 0, sprites.Length - 1);
                return sprites[index];
            }
        }

        [Tooltip("海洋和湖泊图标。")]
        public SpriteVariants water = new();

        [Tooltip("高海拔山地图标。")]
        public SpriteVariants mountains = new();

        [Tooltip("灌木地图标。")]
        public SpriteVariants shrub = new();

        [Tooltip("温带与亚热带沙漠图标。")]
        public SpriteVariants desert = new();

        [Tooltip("热带雨林与季雨林图标。")]
        public SpriteVariants jungle = new();

        [Tooltip("普通温带森林图标。")]
        public SpriteVariants forest = new();

        [Tooltip("草地图标。")]
        public SpriteVariants grassland = new();

        [Tooltip("沼泽图标。")]
        public SpriteVariants marsh = new();

        [Tooltip("针叶林或冬季森林图标。")]
        public SpriteVariants winterForest = new();

        [Tooltip("地图北部温带森林的专用图标。")]
        public SpriteVariants northernForest = new();

        /// <summary>
        /// 根据区域海拔、生物群系和纬度选择图标类别，再用选择值确定具体变体。
        /// </summary>
        public Sprite GetSprite(VoronoiCell cell, float selection, float northernThreshold)
        {
            if (cell.elevation > 0.8f && !cell.isWater) return mountains.Get(selection);
            switch (cell.biome)
            {
                case MapBiome.Ocean:
                case MapBiome.Lake:
                    return water.Get(selection);
                case MapBiome.Shrubland:
                    return shrub.Get(selection);
                case MapBiome.TemperateDesert:
                case MapBiome.SubtropicalDesert:
                    return desert.Get(selection);
                case MapBiome.TropicalRainForest:
                case MapBiome.TropicalSeasonalForest:
                    return jungle.Get(selection);
                case MapBiome.TemperateDeciduousForest:
                case MapBiome.TemperateRainForest:
                    return cell.site.y > northernThreshold ? northernForest.Get(selection) : forest.Get(selection);
                case MapBiome.Grassland:
                    return grassland.Get(selection);
                case MapBiome.Marsh:
                    return marsh.Get(selection);
                case MapBiome.Taiga:
                    return winterForest.Get(selection);
                default:
                    return null;
            }
        }
    }
}
