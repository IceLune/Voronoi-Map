using System.Collections.Generic;
using UnityEngine;

namespace Map
{
    /// <summary>
    /// 各渲染模块共用的材质与基础网格构建方法。
    /// 这里只保留跨地表、边界和河流都会使用的底层辅助逻辑。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        /// <summary>获取支持顶点色的地图材质，并在首次使用时创建。</summary>
        private Material GetGeneratedMaterial()
        {
            if (_generatedMaterial) return _generatedMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            _generatedMaterial = new Material(shader)
            {
                name = "Generated Map Vertex Color Material",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = Texture2D.whiteTexture
            };
            return _generatedMaterial;
        }

        /// <summary>将 0～255 的 RGB 分量转换为 Unity 颜色。</summary>
        private static Color Rgb(int red, int green, int blue)
        {
            return new Color(red / 255f, green / 255f, blue / 255f, 1f);
        }

        /// <summary>向网格追加一段有宽度的带状四边形。</summary>
        private static void AddRibbonSegment(List<Vector3> vertices, List<Color> colors, List<int> triangles,
            Vector2 start, Vector2 end, float width, Color color, float z)
        {
            var direction = end - start;
            if (direction.sqrMagnitude < 0.000001f) return;
            var perpendicular = new Vector2(-direction.y, direction.x).normalized * width;
            var first = vertices.Count;
            vertices.Add(new Vector3(start.x + perpendicular.x, start.y + perpendicular.y, z));
            vertices.Add(new Vector3(start.x - perpendicular.x, start.y - perpendicular.y, z));
            vertices.Add(new Vector3(end.x + perpendicular.x, end.y + perpendicular.y, z));
            vertices.Add(new Vector3(end.x - perpendicular.x, end.y - perpendicular.y, z));
            for (var i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first + 2); triangles.Add(first + 3); triangles.Add(first + 1);
        }
    }
}
