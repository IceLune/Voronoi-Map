using System.Collections.Generic;

namespace Map
{
    /// <summary>
    /// 地图只读结果接口。只需要查询生成数据的系统无需依赖生成控制能力。
    /// </summary>
    public interface IMapResultProvider
    {
        /// <summary>当前生成结果的只读区域集合。</summary>
        IReadOnlyList<VoronoiCell> Cells { get; }

        /// <summary>最近一次成功生成的区域数量。</summary>
        int GeneratedCellCount { get; }

        /// <summary>最近一次成功生成的河源数量。</summary>
        int GeneratedRiverCount { get; }
    }

    /// <summary>
    /// 地图种子和细节变体控制接口，供存档、关卡配置和随机地图入口调用。
    /// </summary>
    public interface IMapSeedController
    {
        /// <summary>当前地图种子。</summary>
        int Seed { get; set; }

        /// <summary>当前细节变体编号。</summary>
        int Variant { get; set; }

        /// <summary>计算新种子并重新生成地图。</summary>
        void NewSeed();
    }

    /// <summary>
    /// 地图生成控制接口。继承只读结果与种子接口，兼容原有统一调用方式。
    /// </summary>
    public interface IMapGenerationController : IMapResultProvider, IMapSeedController
    {
        /// <summary>当前地图规模预设。</summary>
        MapSizePreset SizePreset { get; set; }

        /// <summary>使用当前参数生成地图。</summary>
        void Generate();

        /// <summary>清除当前生成结果。</summary>
        void ClearGeneratedMap();
    }

    /// <summary>
    /// 地图显示模式控制接口，与地形生成职责分离。
    /// </summary>
    public interface IMapDisplayController
    {
        /// <summary>当前显示模式。</summary>
        MapDisplayMode DisplayMode { get; }

        /// <summary>切换到指定显示模式。</summary>
        void SetDisplayMode(MapDisplayMode mode);

        /// <summary>循环切换到下一个显示模式。</summary>
        void CycleDisplayMode();
    }
}
