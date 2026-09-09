using UnityEngine;

namespace Map
{
    /// <summary>
    /// 负责运行时 IMGUI 控制面板与相机视口适配。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        // 运行时控制面板只修改参数并请求对应层重绘，不参与地图算法。
        private void OnGUI()
        {
            if (!Application.isPlaying || !showRuntimeUi) return;
            EnsureGuiStyles();

            var panelRect = new Rect(Screen.width - RuntimePanelWidth, 0f, RuntimePanelWidth, Screen.height);
            var previousColor = GUI.color;
            GUI.color = new Color(0.78f, 0.78f, 0.72f, 1f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            var regenerate = false;
            var redrawMap = false;
            var redrawEdges = false;
            var redrawIcons = false;
            GUILayout.BeginArea(new Rect(panelRect.x + 8f, 6f, panelRect.width - 16f, panelRect.height - 12f), _panelStyle);
            GUILayout.Label("Mapgen2", _titleStyle);
            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed:", GUILayout.Width(48f));
            _seedText = GUILayout.TextField(_seedText ?? seed.ToString(), GUILayout.Width(72f));
            if (GUILayout.Button("-", GUILayout.Width(25f))) { seed = Mathf.Max(0, seed - 1); regenerate = true; }
            if (GUILayout.Button("+", GUILayout.Width(25f))) { seed++; regenerate = true; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply seed", GUILayout.Height(20f)) && int.TryParse(_seedText, out var parsedSeed))
            {
                seed = Mathf.Max(0, parsedSeed);
                regenerate = true;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Variant:", GUILayout.Width(58f));
            _variantText = GUILayout.TextField(_variantText ?? variant.ToString(), GUILayout.Width(47f));
            if (GUILayout.Button("-", GUILayout.Width(25f))) { variant = (variant + 9) % 10; regenerate = true; }
            if (GUILayout.Button("+", GUILayout.Width(25f))) { variant = (variant + 1) % 10; regenerate = true; }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Apply variant", GUILayout.Height(20f)) && int.TryParse(_variantText, out var parsedVariant))
            {
                variant = Mathf.Clamp(parsedVariant, 0, 9);
                regenerate = true;
            }

            GUILayout.Space(7f);
            regenerate |= SliderRow("Dry", "Wet", ref rainfall, -1f, 1f);
            regenerate |= SliderRow("N-Cold", "N-Hot", ref northTemperature, -1.5f, 1.5f);
            regenerate |= SliderRow("S-Cold", "S-Hot", ref southTemperature, -1.5f, 1.5f);
            regenerate |= SliderRow("Jagged", "Smooth", ref persistence, -1f, 1f);

            GUILayout.Space(7f);
            GUILayout.Label("Number of regions:", _smallStyle);
            regenerate |= SizeToggle(MapSizePreset.Tiny, "tiny");
            regenerate |= SizeToggle(MapSizePreset.Small, "small");
            regenerate |= SizeToggle(MapSizePreset.Medium, "medium");
            regenerate |= SizeToggle(MapSizePreset.Large, "large");
            regenerate |= SizeToggle(MapSizePreset.Huge, "huge");

            GUILayout.Space(7f);
            GUILayout.Label("Rendering:", _smallStyle);
            var nextNoisyEdges = GUILayout.Toggle(noisyEdges, "noisy edges");
            var nextShowRegionBorders = GUILayout.Toggle(showRegionBorders, "region borders");
            var nextNoisyFills = GUILayout.Toggle(noisyFills, "noisy fills");
            var nextIcons = GUILayout.Toggle(icons, "icons");
            var nextBiomes = GUILayout.Toggle(biomes, "biomes");
            var nextLighting = GUILayout.Toggle(lighting, "lighting");
            if (nextNoisyEdges != noisyEdges) { noisyEdges = nextNoisyEdges; redrawEdges = true; }
            if (nextShowRegionBorders != showRegionBorders) { showRegionBorders = nextShowRegionBorders; redrawEdges = true; }
            if (nextNoisyFills != noisyFills) { noisyFills = nextNoisyFills; redrawMap = true; }
            if (nextIcons != icons) { icons = nextIcons; redrawIcons = true; }
            if (nextBiomes != biomes) { biomes = nextBiomes; redrawMap = true; redrawEdges = true; }
            if (nextLighting != lighting) { lighting = nextLighting; redrawMap = true; }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Regions: " + generatedCellCount + "   Rivers: " + generatedRiverCount, _smallStyle);
            GUILayout.Label("R: new seed  |  Tab: debug view", _smallStyle);
            GUILayout.Label("Red Blob Games mapgen2 Unity port", _smallStyle);
            GUILayout.EndArea();

            if (regenerate)
            {
                Generate();
            }
            else
            {
                if (redrawMap && _cells.Count > 0) BuildMapMesh();
                if (redrawEdges && _cells.Count > 0) { BuildBorderMesh(); BuildRiverMesh(); }
                if (redrawIcons || regenerate) BuildIconMesh();
            }
        }

        private bool SliderRow(string left, string right, ref float value, float minimum, float maximum)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(left, _smallStyle, GUILayout.Width(43f));
            var next = GUILayout.HorizontalSlider(value, minimum, maximum, GUILayout.Width(92f));
            GUILayout.Label(right, _smallStyle, GUILayout.Width(48f));
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(next, value)) return false;
            value = next;
            return true;
        }

        private bool SizeToggle(MapSizePreset preset, string label)
        {
            var selected = mapSize == preset;
            var next = GUILayout.Toggle(selected, label);
            if (!next || selected) return false;
            mapSize = preset;
            return true;
        }

        private void EnsureGuiStyles()
        {
            if (_panelStyle != null) return;
            _panelStyle = new GUIStyle(GUI.skin.label) { padding = new RectOffset(2, 2, 2, 2) };
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.18f, 0.18f, 0.16f) }
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.20f, 0.20f, 0.18f) }
            };
        }

        // 根据右侧面板宽度调整正交相机视口。
        private void ConfigureCamera()
        {
            if (!Application.isPlaying) return;
            var main = Camera.main;
            if (!main) return;
            var panelWidth = showRuntimeUi ? Mathf.Min(RuntimePanelWidth, Screen.width * 0.45f) : 0f;
            var viewportWidth = Mathf.Max(1f, Screen.width - panelWidth);
            var normalizedWidth = viewportWidth / Mathf.Max(1f, Screen.width);
            main.rect = new Rect(0f, 0f, normalizedWidth, 1f);
            main.orthographic = true;
            main.clearFlags = CameraClearFlags.SolidColor;
            main.backgroundColor = Rgb(0x44, 0x44, 0x7a);
            var viewportAspect = viewportWidth / Mathf.Max(1f, Screen.height);
            var fitHeight = HalfSize.y * 1.025f;
            var fitWidth = HalfSize.x / Mathf.Max(0.01f, viewportAspect) * 1.025f;
            main.orthographicSize = Mathf.Max(fitHeight, fitWidth);
            main.transform.position = new Vector3(0f, 0f, -10f);
            main.transform.rotation = Quaternion.identity;
        }

    }
}
