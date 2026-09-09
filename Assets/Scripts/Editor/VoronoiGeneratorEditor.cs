using Map;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>
    /// 绘制地图生成器的 Inspector
    /// </summary>
    [CustomEditor(typeof(VoronoiGenerator))]
    public sealed class VoronoiGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var generator = (VoronoiGenerator)target;

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成地图", GUILayout.Height(28f)))
                {
                    generator.Generate();
                    EditorUtility.SetDirty(generator);
                }

                if (GUILayout.Button("新种子", GUILayout.Height(28f)))
                {
                    generator.NewSeed();
                    EditorUtility.SetDirty(generator);
                }
            }

            if (GUILayout.Button("清除生成的地图"))
            {
                generator.ClearGeneratedMap();
                EditorUtility.SetDirty(generator);
            }

            EditorGUILayout.HelpBox(
                "Red Blob Mapgen2 Unity 移植版：修复抖动的网格 -> 岛屿噪声 -> 海洋湖泊洪水填充 -> 海岸距离高度 -> 排水和河流 -> 湿度重新分布 -> 纬度高度温度 -> 生物群落。播放模式显示原始种子、变体、气候、大小和渲染控制。",
                MessageType.Info);
            EditorGUILayout.LabelField("Generated cells", generator.GeneratedCellCount.ToString());
            EditorGUILayout.LabelField("River sources", generator.GeneratedRiverCount.ToString());
        }
    }
}