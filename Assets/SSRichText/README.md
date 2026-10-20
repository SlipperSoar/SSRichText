# SSRichText

基于 Unity 旧版 UI Text（UGUI Legacy Text）的富文本组件：在原生 `<b>`、`<i>`、`<size>`、`<color>` 之外，额外支持描边、阴影、下划线、删除线、渐变、链接、内联图标与 GIF 动图，并提供统一的点击回调。

> 扩展的是 `UnityEngine.UI.Text`，**不适用于 TextMeshPro**。

完整文档、示例与更新日志请见仓库主页：<https://github.com/SlipperSoar/SSRichText#readme>

## 环境要求

- Unity 2021.2 及以上（代码使用 C# 9 语法；2021.3.27f1 实测）
- UGUI（`com.unity.ugui`）的 Legacy `Text`
- 无其它运行时依赖

## 安装

`Window > Package Manager` → `+` → `Add package from git URL...`：

```text
https://github.com/SlipperSoar/SSRichText.git?path=/Assets/SSRichText
```

需要固定版本时追加 tag，例如 `#v2.0.0`。

## 使用

1. 在 Canvas 下通过 `GameObject > UI > SS > Rich Text` 创建，或 `Add Component` 搜索 `UI/SS/RichText`。
2. 在 `Text` 中输入标签文本，例如 `<outline=#000000>描边</outline>`、`<icon=heart/>`、`<gif=cat/>`。
3. 订阅点击事件：

```csharp
using SS.UIComponent;
using UnityEngine;

public class RichTextExample : MonoBehaviour
{
    [SerializeField] private RichText richText;

    private void Start()
    {
        richText.OnClick += (type, message) =>
        {
            if (type == RichText.RichType.Link)
            {
                Application.OpenURL(message);
            }
        };
    }
}
```

## 支持的标签

| 标签 | 示例 | 说明 |
| --- | --- | --- |
| 粗体 | `<b>text</b>` | 原生 |
| 斜体 | `<i>text</i>` | 原生 |
| 字号 | `<size=24>text</size>` | 原生 |
| 颜色 | `<color=#ff8844>text</color>` | 原生，支持颜色名与 6/8 位十六进制 |
| 删除线 | `<s>text</s>` | 颜色跟随文字颜色 |
| 描边 | `<outline=red>text</outline>` | 必须带颜色参数 |
| 阴影 | `<shadow=rb>text</shadow>` | `lt` / `rt` / `lb` / `rb` 四个方向 |
| 下划线 | `<underline=red>text</underline>` | 必须带颜色参数 |
| 渐变 | `<gradient lt=red rt=red lb=red rb=red>text</gradient>` | 四个角的颜色 |
| 链接 | `<link=www.unity.com>text</link>` | 支持省略协议头，回调时自动补 `https://` |
| 图标 | `<icon=heart/>` | 默认从 `Resources` 读取 `Sprite` |
| 动图 | `<gif=cat/>` | 默认从 `Resources` 读取 `.bytes` |

## 点击事件

```csharp
public event Action<RichText.RichType, string> OnClick;
```

`<link>` 返回标签中的地址（不带协议头时会补全成 `https://...`），`<icon>`、`<gif>` 返回图标或动图名称，其它标签返回被包裹的文本。标签重叠时按 `RichType` 枚举顺序触发优先级最高者。

## 图标与动图

- 图标：实现 `IIconProvider` 后通过 `richText.SetIconProvider(provider)` 或 `RichText.GlobalIconProvider` 替换默认的 `Resources` 加载。
- 动图：导入 `.gif` 时会自动转成 `.bytes`；运行时由 `GifLoadManager`（自动创建、`DontDestroyOnLoad`）解析与播放，也可通过 `IGifProvider`、`AddGifData` 接入自定义来源。

## 许可证

[MIT](https://github.com/SlipperSoar/SSRichText/blob/main/LICENSE)
