using UnityEngine;

namespace Map
{
    /// <summary>
    /// 集中存放稳定哈希、边键、对象释放等跨模块工具。
    /// </summary>
    public sealed partial class VoronoiGenerator
    {
        private static int HashVector(Vector2 value)
        {
            unchecked
            {
                var x = Mathf.RoundToInt(value.x * 10000f);
                var y = Mathf.RoundToInt(value.y * 10000f);
                return x * 73856093 ^ y * 19349663;
            }
        }

        // 稳定哈希用于可重复的局部随机选择，不消耗 Unity 全局随机状态。
        private static float Hash01(int value, int salt)
        {
            unchecked
            {
                var hash = (uint)(value ^ (salt * 0x45d9f3b));
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                hash *= 0x846ca68b;
                hash ^= hash >> 16;
                return (hash & 0x00ffffff) / 16777215f;
            }
        }

        // 将无向边编码为顺序无关的 64 位键。
        private static long EdgeKey(int a, int b)
        {
            var low = (uint)Mathf.Min(a, b); var high = (uint)Mathf.Max(a, b);
            return ((long)high << 32) | low;
        }

        private static void DecodeEdgeKey(long key, out int a, out int b)
        {
            a = (int)(key & 0xffffffffL); b = (int)((ulong)key >> 32);
        }

        private static void DestroyGeneratedObject<T>(ref T value) where T : Object
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
            value = null;
        }

    }
}
