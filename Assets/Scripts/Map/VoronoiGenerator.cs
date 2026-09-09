using System;
using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// Mapgen2 的 Unity 入口组件。仅负责生命周期、参数和生成流水线编排；
    /// 具体算法按职责拆分到同名 partial 文件中。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed partial class VoronoiGenerator : MonoBehaviour, IMapGenerationController, IMapDisplayController
    {
        [Header("布局")]
        [Tooltip("地图规模预设。生成前会将其转换为对应的区域数量。")]
        public MapSizePreset mapSize = MapSizePreset.Medium;
        [Tooltip("Voronoi 站点数量；使用规模预设时会由预设覆盖。")]
        [Min(32)] public int pointCount = 3000;
        [Tooltip("地图在世界坐标中的宽度和高度。")]
        public Vector2 size = new(24f, 24f);
        [Tooltip("Lloyd 松弛迭代次数，用于均匀化区域大小。")]
        [Range(0, 4)] public int relaxationIterations = 1;
        [Tooltip("规则采样点的随机偏移强度。")]
        [Range(0f, 0.95f)] public float pointJitter = 0.72f;
        [Tooltip("地图种子。相同参数、种子和变体会生成相同结果。")]
        public int seed = 187;
        [Tooltip("同一种子的图标与局部细节变体编号。")]
        [Range(0, 9)] public int variant;

        [Header("地形")]
        [Tooltip("海平面阈值，数值越高水域通常越多。")]
        [Range(0.2f, 0.75f)] public float seaLevel = 0.5f;
        [Tooltip("岛屿轮廓趋向圆形的程度。")]
        [Range(0f, 1f)] public float islandRoundness = 0.5f;
        [Tooltip("岛屿边缘受噪声扰动的程度。")]
        [Range(0f, 1f)] public float islandIrregularity = 0.4f;
        [Tooltip("地形噪声的采样尺度。")]
        [Min(0.001f)] public float noiseScale = 0.075f;
        [Tooltip("地形噪声对最终海拔的影响强度。")]
        [Range(0f, 1f)] public float noiseStrength = 0.42f;

        [Header("气候")]
        [Tooltip("期望选择的河源数量。实际数量受地形条件限制。")]
        [Range(0, 80)] public int riverCount = 30;
        [Tooltip("河流向周围陆地传播湿度的最大步数。")]
        [Range(2, 20)] public int moistureReach = 9;
        [Tooltip("河流基础半宽，最终宽度还会随流量变化。")]
        [Range(0.008f, 0.12f)] public float riverWidth = 0.025f;
        [Tooltip("整体降雨偏移量。")]
        [Range(-1f, 1f)] public float rainfall;
        [Tooltip("地图北侧的温度偏移量。")]
        [Range(-1.5f, 1.5f)] public float northTemperature;
        [Tooltip("地图南侧的温度偏移量。")]
        [Range(-1.5f, 1.5f)] public float southTemperature;
        [Tooltip("气候噪声的持续性偏移量。")]
        [Range(-1f, 1f)] public float persistence;

        [Header("生成")]
        [Tooltip("组件启用时自动生成地图。")]
        public bool generateOnEnable = true;
        [Tooltip("运行时按 R 键生成新种子地图。")]
        public bool regenerateWithRKey = true;
        [Tooltip("当前地图显示模式。")]
        public MapDisplayMode displayMode = MapDisplayMode.FullMap;
        [Tooltip("为普通区域边界添加稳定的折线噪声。")]
        public bool noisyEdges = true;
        [Tooltip("显示所有区域之间的细边界。海岸线和湖岸线不受此开关影响。")]
        public bool showRegionBorders;
        [Tooltip("为地表颜色添加稳定的细微明暗变化。")]
        public bool noisyFills = true;
        [Tooltip("在完整地图模式中显示地形图标。")]
        public bool icons = true;
        [Tooltip("使用离散生物群系配色，而不是平滑地形配色。")]
        public bool biomes;
        [Tooltip("根据邻接区域海拔梯度添加简易浮雕明暗。")]
        public bool lighting;
        [Tooltip("运行时显示内置 IMGUI 调试面板。")]
        public bool showRuntimeUi = true;

        [Header("图标资源")]
        [Tooltip("独立 Sprite 图标配置；为空时尝试从 Resources/MapIconLibrary 加载。")]
        public MapIconLibrary iconLibrary;

        [SerializeField, HideInInspector] private int generatedCellCount;
        [SerializeField, HideInInspector] private int generatedRiverCount;

        private readonly List<VoronoiCell> _cells = new();
        private readonly HashSet<long> _riverEdges = new();
        private readonly List<RiverCorner> _riverCorners = new();
        private readonly Dictionary<long, RiverBoundaryEdge> _riverBoundaryEdges = new();
        private readonly HashSet<int> _riverCells = new();
        private int _selectedRiverSourceCount;
        private Mesh _mapMesh;
        private Mesh _riverMesh;
        private Mesh _borderMesh;
        private Mesh _iconMesh;
        private Material _generatedMaterial;
        private Material _iconMaterial;
        private GameObject _riverObject;
        private GameObject _borderObject;
        private GameObject _iconObject;
        private string _seedText;
        private string _variantText;
        private GUIStyle _panelStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _smallStyle;
        private const float RuntimePanelWidth = 210f;

        /// <summary>当前生成结果的只读区域集合。</summary>
        public IReadOnlyList<VoronoiCell> Cells => _cells;
        /// <summary>最近一次成功执行生成流水线得到的区域数量。</summary>
        public int GeneratedCellCount => generatedCellCount;

        /// <summary>最近一次成功执行生成流水线选择的河源数量。</summary>
        public int GeneratedRiverCount => generatedRiverCount;

        /// <summary>当前地图种子；赋值时保证非负。</summary>
        public int Seed { get => seed; set => seed = Mathf.Max(0, value); }

        /// <summary>当前细节变体；赋值时限制到 0～9。</summary>
        public int Variant { get => variant; set => variant = Mathf.Clamp(value, 0, 9); }

        /// <summary>当前地图规模预设。</summary>
        public MapSizePreset SizePreset { get => mapSize; set => mapSize = value; }

        /// <summary>当前地图显示模式。</summary>
        public MapDisplayMode DisplayMode => displayMode;

        private Vector2 HalfSize => new(size.x * 0.5f, size.y * 0.5f);

        #region 生命周期

        private void OnEnable()
        {
            _seedText = seed.ToString();
            _variantText = variant.ToString();
            ConfigureCamera();
            if (generateOnEnable)
            {
                Generate();
            }
        }

        private void Update()
        {
            if (Application.isPlaying) ConfigureCamera();
            if (Application.isPlaying && regenerateWithRKey && Input.GetKeyDown(KeyCode.R))
            {
                NewSeed();
            }

            if (Application.isPlaying && Input.GetKeyDown(KeyCode.Tab))
            {
                CycleDisplayMode();
            }
        }

        private void OnValidate()
        {
            pointCount = Mathf.Max(32, pointCount);
            size.x = Mathf.Max(4f, size.x);
            size.y = Mathf.Max(4f, size.y);
            riverWidth = Mathf.Max(0.008f, riverWidth);
            variant = Mathf.Clamp(variant, 0, 9);
        }

        #endregion

        /// <summary>
        /// 按固定阶段生成完整地图。阶段顺序和随机数调用顺序属于生成结果的一部分，
        /// 重构时不得随意调整。
        /// </summary>
        [ContextMenu("Generate Map")]
        public void Generate()
        {
            ClampSettings();
            var previousState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(12345 ^ pointCount);

                // 1. 创建站点并执行 Lloyd 松弛，得到最终 Voronoi 区域。
                var sites = CreateSites();
                for (var iteration = 0; iteration < relaxationIterations; iteration++)
                {
                    BuildCells(sites);
                    for (var i = 0; i < _cells.Count; i++)
                    {
                        sites[i] = PolygonCentroid(_cells[i].vertices, _cells[i].site);
                    }
                }

                BuildCells(sites);

                // 2. 依次计算地形、水文、湿度与生物群系。
                AssignTerrain();
                BuildDrainage();
                SelectRivers();
                AssignMoisture();
                AssignBiomes();

                // 3. 按固定层级构建地表、河流、边界与图标网格。
                BuildMapMesh();
                BuildRiverMesh();
                BuildBorderMesh();
                BuildIconMesh();

                // 4. 仅在全部阶段完成后发布统计结果和运行时文本。
                generatedCellCount = _cells.Count;
                generatedRiverCount = CountRiverSources();
                _seedText = seed.ToString();
                _variantText = variant.ToString();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                UnityEngine.Random.state = previousState;
            }
        }

        /// <summary>使用与旧实现一致的线性同余步骤产生新种子并重新生成。</summary>
        [ContextMenu("Generate With New Seed")]
        public void NewSeed()
        {
            seed = unchecked(seed * 1103515245 + 12345);
            if (seed < 0) seed = -(seed + 1);
            Generate();
        }

        /// <summary>清除当前生成数据、网格和运行时子对象。</summary>
        [ContextMenu("Clear Generated Map")]
        public void ClearGeneratedMap()
        {
            _cells.Clear();
            _riverEdges.Clear();
            _riverCorners.Clear();
            _riverBoundaryEdges.Clear();
            _riverCells.Clear();
            _selectedRiverSourceCount = 0;
            generatedCellCount = 0;
            generatedRiverCount = 0;
            var filter = GetComponent<MeshFilter>();
            if (filter != null) filter.sharedMesh = null;
            DestroyGeneratedObject(ref _mapMesh);
            DestroyGeneratedObject(ref _riverMesh);
            if (_riverObject != null) DestroyGeneratedObject(ref _riverObject);
            DestroyGeneratedObject(ref _borderMesh);
            if (_borderObject != null) DestroyGeneratedObject(ref _borderObject);
            DestroyGeneratedObject(ref _iconMesh);
            if (_iconObject != null) DestroyGeneratedObject(ref _iconObject);
        }

        /// <summary>切换显示模式，并刷新受该模式影响的可见对象。</summary>
        public void SetDisplayMode(MapDisplayMode mode)
        {
            displayMode = mode;
            if (_cells.Count > 0) BuildMapMesh();
            if (_borderObject != null) _borderObject.SetActive(displayMode == MapDisplayMode.FullMap);
            if (_riverObject != null) _riverObject.SetActive(displayMode == MapDisplayMode.FullMap || displayMode == MapDisplayMode.Rivers);
            if (_iconObject != null) _iconObject.SetActive(icons && displayMode == MapDisplayMode.FullMap);
        }

        /// <summary>循环切换到枚举中的下一个显示模式。</summary>
        public void CycleDisplayMode()
        {
            var next = ((int)displayMode + 1) % Enum.GetValues(typeof(MapDisplayMode)).Length;
            SetDisplayMode((MapDisplayMode)next);
        }

        private void ClampSettings()
        {
            ApplySizePreset();
            pointCount = Mathf.Clamp(pointCount, 32, 20000);
            size = new Vector2(Mathf.Max(4f, size.x), Mathf.Max(4f, size.y));
            seaLevel = Mathf.Clamp(seaLevel, 0.2f, 0.75f);
            moistureReach = Mathf.Max(2, moistureReach);
        }

        private void ApplySizePreset()
        {
            switch (mapSize)
            {
                case MapSizePreset.Tiny: pointCount = 700; break;
                case MapSizePreset.Small: pointCount = 1500; break;
                case MapSizePreset.Medium: pointCount = 3000; break;
                case MapSizePreset.Large: pointCount = 6000; break;
                case MapSizePreset.Huge: pointCount = 12000; break;
            }
        }

    }
}
