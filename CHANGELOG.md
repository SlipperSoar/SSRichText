# Changelog

本项目的版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)，版本记录来自 [GitHub Releases](https://github.com/SlipperSoar/SSRichText/releases)。

## [Unreleased]

## [2.3.1] - 2026-10-20

### Fixed

- 修复 `RichTextIconImage` 每次重建都申请新的合并 `RenderTexture` 却从不释放的问题。
- 修复 GIF 缓存淘汰、`AddGifData` 替换、GIF Viewer 停止/关闭时帧纹理没有销毁的问题；解码中途取消也会释放已解码帧。
- 修复 GIF Viewer 加载完成后的预览不会自动播放的问题，现在按约 30fps 自动重绘。
- 移除 `RichTextIconImage` 的调试/逐帧日志，避免编辑器 Console 刷屏。

### Changed

- `GifLoadManager.Update` 复用清理列表，避免每帧产生新的 `List` 分配。
- `GifDecoder.Decode` 新增接收帧列表的重载，便于调用方管理帧的生命周期。

## [2.3.0] - 2026-10-20

### Changed

- `RichText.cs` 拆分为四个 partial 文件：主组件、`RichText.Parser.cs`（解析）、`RichText.Mesh.cs`（网格效果）、`RichText.Types.cs`（类型与数据结构）。
- 增加文本解析缓存：文本未变化时复用解析结果，不再重复执行正则和标签处理；每次重建克隆一份数据，避免网格构建与图标更新协程互相影响。
- 移除未使用的 `spaceWidth` 字段。

## [2.2.0] - 2026-10-20

### Added

- 图标与 GIF 名称支持 `Resources` 子目录：`<icon=Icons/heart/>` 对应 `Resources.Load<Sprite>("Icons/heart")`，`<gif=Gifs/cat/>` 对应 `Resources/Gifs/cat.bytes`；写反斜杠会自动转成正斜杠。
- 新增英文文档 [README_EN.md](README_EN.md)，中英文 README 顶部可以互相切换。

### Changed

- GIF 帧上传从逐像素 `SetPixel` 改为一次性 `SetPixels32`，帧数多时解码速度明显提升。
- 解码过程的详细日志默认关闭，可用 `GifDecoder.VerboseLog` 打开；`GIF Viewer` 加载改为在时间预算内批量推进解码，预览速度明显提升。
- 文档修正：GIF 的纯文本扩展块（Plain Text Extension）是文件内的元数据（常见于版权、说明等），不是叠加在动图上的文字，也不是“未实现的渲染功能”。

### Fixed

- 修复 GIF Viewer 加载多帧动图过慢的问题。

## [2.1.0] - 2026-10-20

### Added

- `<link>` 支持省略协议头的地址（`www.example.com`、`example.com/path`、`localhost:8080`、`127.0.0.1:8080`），点击回调时会自动补全为 `https://...`；同时支持 `mailto:`、`tel:` 等非层级协议。

### Changed

- Editor 代码改为独立程序集 `SS.UIComponent.Editor`（`includePlatforms: Editor`），不再用整文件 `#if UNITY_EDITOR` 包裹，也不会被编进运行时程序集。
- GIF 预览窗口菜单从 `Window > GIF Viewer` 调整为 `Window > SS > GIF Viewer`。
- `.idea/` 目录不再纳入版本管理。

### Fixed

- GIF 预览窗口重新打开后可能空引用、重复注册 `EditorApplication.update` 回调、"Stop Load" 后同一文件无法再次加载的问题。
- 运行时程序集里的 `using UnityEditor;` 改为仅在编辑器下生效。

## [2.0.0] - 2026-01-22

### Fixed

- 修复 Editor 脚本在打包（Build）过程中报编译错误的问题。**旧版本打包报错的请升级到此版本。**

## [1.9.0] - 2026-01-08

### Added

- 新增 `dontClear` 标识，可以让 GIF 缓存不随闲置超时被自动清理。
- 新增用帧动画构造 GIF 的方法（`GifLoadManager.AddGifData`）。

## [1.8.0] - 2026-01-05

### Changed

- GIF 的加载与显示分离。
- GIF 的尺寸在加载时缓存，避免重复读取 GIF 的字节数据。

## [1.7.0] - 2026-01-05

### Added

- GIF 加载不再局限于 `Resources`，新增 `IGifProvider` / `GifProvider`，可根据项目情况自定义加载逻辑；未指定时仍默认从 `Resources` 读取。

## [1.6.0] - 2026-01-04

### Changed

- **阴影标签格式更新**：`<shadow>` 需要标明阴影方向，共有 `lt`、`rt`、`lb`、`rb` 四角四个方向。

### Added

- 图标阴影支持四个方向，并会根据实际大小动态调整。
- 颜色支持 7 位十六进制代码：最后一位会自动补全为 alpha，例如 `#ff00ffa` 会被补全为 `#ff00ffaa`。

## [1.5.0] - 2026-01-04

### Fixed

- 修复修改非原生标签（例如下划线颜色）时文本渲染错乱的问题。

## [1.4.0] - 2025-12-31

### Added

- 新增 `<gradient>` 渐变标签。

## [1.3.0] - 2025-12-31

### Changed

- GIF 播放改版：由各组件独立播放改为 `GifLoadManager` 统一播放。

## [1.2.0] - 2025-12-30

### Changed

- 优化阴影显示：根据实际字体大小动态调整，避免字体过大时阴影不明显。
- 优化描边显示：根据实际字体大小动态调整，避免字体过大时描边不明显。

## [1.1.0] - 2025-12-30

### Added

- 支持删除线标签 `<s></s>`。

## [1.0.0] - 2025-05-02

### Added

- 首个版本：基于 Unity 原生 UI Text 的富文本组件。

[Unreleased]: https://github.com/SlipperSoar/SSRichText/compare/v2.3.1...HEAD
[2.3.1]: https://github.com/SlipperSoar/SSRichText/compare/v2.3.0...v2.3.1
[2.3.0]: https://github.com/SlipperSoar/SSRichText/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/SlipperSoar/SSRichText/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/SlipperSoar/SSRichText/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.9.0...v2.0.0
[1.9.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.8.0...v1.9.0
[1.8.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.7.0...v1.8.0
[1.7.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.6.0...v1.7.0
[1.6.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.5.0...v1.6.0
[1.5.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/SlipperSoar/SSRichText/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/SlipperSoar/SSRichText/releases/tag/v1.0.0
