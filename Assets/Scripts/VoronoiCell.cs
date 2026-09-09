using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>地图区域使用的离散生物群系分类。</summary>
public enum MapBiome
{
    Ocean, // 海洋
    Lake, // 湖泊
    Marsh, // 泠泽
    Ice, // 冰雪
    Beach, // 沙滩
    Snow, // 雪地
    Tundra, // 草原
    Bare, // 荒地
    Scorched, // 烈地
    Taiga, // 寒林
    Shrubland, // 灌木丛
    TemperateDesert, // 温带沙漠
    TemperateRainForest, // 温带雨林
    TemperateDeciduousForest, // 温带落叶林
    Grassland, // 草原
    TropicalRainForest, // 热带雨林
    TropicalSeasonalForest, // 热带季雨林
    SubtropicalDesert // 亚热带沙漠
}

/// <summary>地图规模预设；生成器会将预设转换为固定站点数量。</summary>
public enum MapSizePreset
{
    Tiny, // 极小
    Small, // 小型
    Medium, // 中型
    Large, // 大型
    Huge // 巨型
}

/// <summary>地图网格支持的可视化模式。</summary>
public enum MapDisplayMode
{
    FullMap, // 全图
    Elevation, // 海拔
    Moisture, // 湿度
    Rivers, // 河流
    Voronoi // 墨菲
}

/// <summary>
/// 单个 Voronoi 区域的生成结果数据。
/// 集合字段在生成阶段内部填充，对外通过生成器的只读集合访问。
/// </summary>
[Serializable]
public sealed class VoronoiCell
{
    /// <summary>区域在当前地图中的稳定索引。</summary>
    public int id;

    /// <summary>用于构造该 Voronoi 区域的站点坐标。</summary>
    public Vector2 site;

    /// <summary>按多边形绕序排列的区域顶点。</summary>
    public List<Vector2> vertices = new();

    /// <summary>与当前区域共享 Delaunay 边的区域索引。</summary>
    public List<int> neighbors = new();

    /// <summary>归一化海拔。</summary>
    public float elevation;

    /// <summary>归一化温度。</summary>
    public float temperature;

    /// <summary>归一化湿度。</summary>
    public float moisture;

    /// <summary>经过该区域的累计水流量。</summary>
    public float flow;

    /// <summary>下坡流向的邻接区域索引；没有下坡方向时为 -1。</summary>
    public int downslope = -1;

    /// <summary>是否为任意水域。</summary>
    public bool isWater;

    /// <summary>是否为与地图边界连通的海洋。</summary>
    public bool isOcean;

    /// <summary>是否为与水域相邻的陆地区域。</summary>
    public bool isCoast;

    /// <summary>区域最终生物群系。</summary>
    public MapBiome biome;
}
