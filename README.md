# Voronoi Map - Mapgen2 Unity

这是一个使用 Unity 和 C# 实现的程序化多边形地图生成项目。项目以 Voronoi 区域为基础，生成岛屿、海洋、湖泊、海拔、气候、生物群系、河流以及地图图标。

本项目是学习和研究性质的非官方 Unity 移植版本。

![Preview](D:\Work\Unity\Voronoi-Map\Preview.png)

## 主要功能

- 使用 Delaunay 三角剖分和 Voronoi 多边形生成地图区域。
- 根据种子生成岛屿轮廓，并区分陆地、海洋、湖泊和海岸。
- 生成海拔、温度、湿度和生物群系。
- 根据地形排水关系生成河流，河流沿 Voronoi 公共边流动。
- 支持完整地图、海拔、湿度、河流和 Voronoi 等显示模式。
- 将地图图集切割为独立 Sprite，并通过 `MapIconLibrary` 配置不同地形图标。
- 支持地图规模、种子、气候和渲染效果调整。

## 使用方法

1. 使用 Unity `2022.3.62f3c1` 或兼容版本打开项目。
2. 打开 `Assets/Scenes/SampleScene.unity`。
3. 进入 Play Mode 查看地图。
4. 可以在 `VoronoiGenerator` 的 Inspector 中修改参数并重新生成。

运行时快捷键：

- `R`：使用新种子重新生成地图。
- `Tab`：循环切换地图显示模式。

## 代码结构

地图生成器使用 C# `partial` 按职责拆分：

- `VoronoiGenerator.cs`：参数、生命周期、公共接口和生成流程。
- `VoronoiGenerator.Geometry.cs`：站点、Delaunay 邻接和 Voronoi 多边形。
- `VoronoiGenerator.Terrain.cs`：陆海分类、海拔、温度和生物群系。
- `VoronoiGenerator.Hydrology.cs`：排水、流量、河流和湿度传播。
- `VoronoiGenerator.Rendering.*.cs`：地表、边界、河流和图标渲染。
- `VoronoiGenerator.RuntimeUI.cs`：运行时控制面板与相机适配。

## 原算法出处

本项目的核心地图生成思路和整体处理流程来自 **Amit Patel / Red Blob Games** 的 Mapgen2 多边形地图生成器：

- Mapgen2 JavaScript 项目：[redblobgames/mapgen2](https://github.com/redblobgames/mapgen2)
- 原始 Polygon Map Generator：[amitp/mapgen2](https://github.com/amitp/mapgen2)
- Red Blob Games 相关文章：[Polygonal Map Generation for Games](https://www.redblobgames.com/maps/mapgen2/)

本项目参考并移植了其中的 Voronoi 区域、岛屿水域分类、海洋洪水填充、海岸距离海拔、排水与河流、湿度、温度、生物群系、噪声边界和地图图标等算法流程，同时针对 Unity 的网格与 Sprite 工作方式进行了调整。

原 Mapgen2 项目和地图图标采用 **Apache License 2.0**。完整许可证见：

`Assets/ThirdParty/Mapgen2_LICENSE.txt`

请注意：本仓库不是 Red Blob Games 官方维护的 Unity 版本。

## 许可证

第三方算法和资源仍遵循各自的原始许可证。发布或再分发本项目时，请保留原作者署名及 `Assets/ThirdParty` 中的许可证文件。
