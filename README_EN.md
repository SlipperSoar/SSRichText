# SSRichText

[简体中文](README.md) | **English**

[![Release](https://img.shields.io/github/v/release/SlipperSoar/SSRichText?sort=semver&label=release)](https://github.com/SlipperSoar/SSRichText/releases/latest)
[![License](https://img.shields.io/github/license/SlipperSoar/SSRichText)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-2021.2%2B-black?logo=unity)](https://unity.com/)
[![Stars](https://img.shields.io/github/stars/SlipperSoar/SSRichText?style=flat)](https://github.com/SlipperSoar/SSRichText/stargazers)

A rich text component for Unity's legacy UGUI `Text`. On top of the built-in `<b>`, `<i>`, `<size>` and `<color>` tags it adds **outline, shadow, underline, strikethrough, gradient, link, inline icons and GIF animations**, plus a single click event for all of them.

> This package extends `UnityEngine.UI.Text` and is **not compatible with TextMeshPro**.

![Preview](DocImages/PixPin_2025-12-31_18-25-59.png)

## Features

- **Tags as layout** — outline, shadow, underline, strikethrough and four-corner gradients are written directly in the text; no extra UI nodes required.
- **Inline icons and GIFs** — `<icon=name/>` and `<gif=name/>` occupy one character cell and scale with the font size. Icons and GIFs are merged into a temporary texture instead of creating an `Image` per icon.
- **One click event for everything** — every tag shares the same `OnClick` callback, which receives the tag type and its content, so links, icons and mixed text are easy to handle.
- **Pluggable asset sources** — `IIconProvider` / `IGifProvider` let you load icons and GIFs from atlases, asset bundles, remote servers, etc.
- **Automatic GIF management** — `GifLoadManager` is created on first use, decodes in the background, plays frames in one place and releases unused data.
- Inherits from `Text`, so font, font size, alignment, wrapping, `Raycast Target` and other properties keep working.

## Requirements

| Item | Requirement |
| --- | --- |
| Unity | 2021.2 or newer (C# 9 is required; tested with 2021.3.27f1) |
| UI system | UGUI (`com.unity.ugui`) legacy `Text` |
| Other dependencies | None at runtime, and no TextMeshPro dependency |

## Installation

### Option 1: UPM (recommended)

1. Open `Window > Package Manager`
2. Click `+` → `Add package from git URL...`
3. Enter:

```text
https://github.com/SlipperSoar/SSRichText.git?path=/Assets/SSRichText
```

To pin a version, append a tag:

```text
https://github.com/SlipperSoar/SSRichText.git?path=/Assets/SSRichText#v2.2.0
```

Or add it to `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.github-slippersoar.ssrichtext": "https://github.com/SlipperSoar/SSRichText.git?path=/Assets/SSRichText#v2.2.0"
  }
}
```

### Option 2: .unitypackage

Download `SSRichText.unitypackage` from [Releases](https://github.com/SlipperSoar/SSRichText/releases/latest) and drag it into the Unity editor.

### Option 3: Clone the repository

The repository itself is a runnable sample project (Unity 2021.3.27f1). Clone it, open it with Unity and play `Assets/Scenes/SampleScene.unity` to see every tag in action.

## Quick start

1. In a Canvas, create the component via `GameObject > UI > SS > Rich Text`, or add it to an existing UI object with `Add Component` → `UI/SS/RichText`.
2. Enter tagged text in the `Text` field, for example:

```text
<color=#ff8844>Notice</color><b>Server</b><i>maintenance</i>
<outline=#000000>outline</outline> <underline=#00ff00>underline</underline> <s>strikethrough</s>
<shadow=rb>shadow</shadow> <link=https://unity.com>website</link> <link=www.unity.com>scheme-less link</link>
<gradient lt=#ff0000 rt=#ffff00 lb=#00ff00 rb=#0000ff>gradient</gradient>
<icon=heart/> <gif=cat/>
```

3. Subscribe to the click event:

```csharp
using SS.UIComponent;
using UnityEngine;

public class RichTextExample : MonoBehaviour
{
    [SerializeField] private RichText richText;

    private void Start()
    {
        richText.OnClick += OnRichTextClick;
    }

    private void OnDestroy()
    {
        richText.OnClick -= OnRichTextClick;
    }

    private void OnRichTextClick(RichText.RichType type, string message)
    {
        switch (type)
        {
            case RichText.RichType.Link:
                Application.OpenURL(message);
                break;
            default:
                Debug.Log($"[{type}] {message}");
                break;
        }
    }
}
```

## Tag reference

| Tag | Example | Notes |
| --- | --- | --- |
| Bold | `<b>text</b>` | Built-in Unity tag |
| Italic | `<i>text</i>` | Built-in Unity tag |
| Size | `<size=24>text</size>` | Built-in Unity tag |
| Color | `<color=red>text</color>` | Built-in Unity tag |
| Strikethrough | `<s>text</s>` | Uses the current text color; line width scales with font size |
| Outline | `<outline=red>text</outline>` | Color is required; accepts color names or hex |
| Shadow | `<shadow=lt>text</shadow>` | Directions: `lt`, `rt`, `lb`, `rb` |
| Underline | `<underline=red>text</underline>` | Color is required |
| Gradient | `<gradient lt=red rt=red lb=red rb=red>text</gradient>` | Four corner colors, each a color name or hex |
| Link | `<link=www.unity.com>text</link>` | Schemes are optional; `https://` is added automatically |
| Icon | `<icon=Icons/heart/>` | Loaded from `Resources` by default (subfolders supported) |
| GIF | `<gif=Gifs/cat/>` | Loaded from `Resources` by default (subfolders supported) |

### Colors

| Form | Example |
| --- | --- |
| Color name | `white`, `black`, `red`, `green`, `blue`, `yellow`, `cyan`, `magenta`, `gray`, `grey` |
| 6-digit hex | `#ff8844` (opaque) |
| 8-digit hex | `#ff884480` (with alpha) |
| 7-digit hex | `#ff88448` (the last digit is repeated as alpha) |

> Hex letters are case-insensitive. Unparsable colors log an error and fall back to transparent.

## Click event

Every tag shares one event:

```csharp
public event Action<RichText.RichType, string> OnClick;
```

| Tag | `type` | `message` |
| --- | --- | --- |
| `<link=url>` | `RichType.Link` | The URL, normalized with `https://` when no scheme was given |
| `<icon=name/>` | `RichType.Icon` | Icon name |
| `<gif=name/>` | `RichType.Gif` | GIF name |
| Other tags | Matching `RichType` | The text wrapped by the tag |

Notes:

- When multiple tags overlap at the same position, the highest priority wins, following the `RichType` enum order: strikethrough > GIF > icon > link > underline > shadow > outline > gradient > size > color > bold > italic.
- `<link>` accepts both full URLs (`http://`, `https://`, `file://`, `mailto:`, `tel:`, custom schemes) and scheme-less addresses (`www.unity.com`, `unity.com/docs`, `localhost:8080`, `127.0.0.1:8080`). For scheme-less addresses the callback receives the normalized `https://...` URL, ready for `Application.OpenURL`.
- Content that is neither a scheme nor a domain, such as `<link=hello>`, is not treated as a link and is displayed as plain text.
- `Raycast Target` must be enabled on the text and a `GraphicRaycaster` must exist on the Canvas to receive clicks.

## Icons

The default `IconProvider` loads a `Sprite` from `Resources` by name: `<icon=heart/>` is `Resources.Load<Sprite>("heart")`, so the texture must be imported as a `Sprite`.

Provide a custom source by implementing `IIconProvider`:

```csharp
using SS.UIComponent;
using UnityEngine;

public class MyIconProvider : IIconProvider
{
    public Sprite GetIcon(string iconName)
    {
        return MyAtlas.Get(iconName); // atlas / asset bundle / remote loading
    }
}

// Per component
richText.SetIconProvider(new MyIconProvider());

// Or set the global default
RichText.GlobalIconProvider = new MyIconProvider();
```

Notes:

- Icon names may contain letters, digits and `_`, `-`, `(`, `)`, `.`, `/`. Subfolders are supported: `<icon=Icons/heart/>` is `Resources.Load<Sprite>("Icons/heart")`, and `Icons\heart` is normalized to `/`. Spaces are not supported, and the extension must be omitted (`heart`, not `heart.png`).
- An icon occupies one character cell and scales with the font size. When the icon is missing, the placeholder character stays transparent instead of showing a box.
- `<shadow>` also applies to icons, with all four directions.

## GIF animations

1. Drag a `.gif` into the project: `GifImporter` converts it into a `.bytes` file with the same name and **deletes the original `.gif`** (commit it first if you need to keep it).
2. Place the `.bytes` anywhere under a `Resources` folder; subfolders are supported. For example, `Assets/Resources/Gifs/cat.bytes` is referenced as `<gif=Gifs/cat/>`, while `Assets/Resources/cat.bytes` is `<gif=cat/>`.
3. Use `<gif=Gifs/cat/>` in the text (the path is relative to `Resources`; `Gifs\cat` also works, and the `.bytes` extension is not needed).

Runtime behavior:

- On first use a `GifLoadManager` is created automatically (`DontDestroyOnLoad`), decodes the bytes in a coroutine and plays frames in one place.
- GIF data that has not been used for 120 seconds is released; pass `dontClear: true` to keep it resident.
- Custom byte sources: implement `IGifProvider.GetGifBytes(string gifName)` and assign it to `GifLoadManager.Instance.GifProvider`.
- Frame sequences can also be injected directly with `AddGifData`:

```csharp
// 1. Custom byte source
public class MyGifProvider : IGifProvider
{
    public byte[] GetGifBytes(string gifName)
    {
        return MyBundle.LoadGifBytes(gifName);
    }
}

GifLoadManager.Instance.GifProvider = new MyGifProvider();

// 2. Provide frames directly
GifLoadManager.Instance.AddGifData(
    gifName: "myAnim",
    size: new Vector2Int(128, 128),
    gifFrames: frames,      // IEnumerable<GifData>, texture + delay per frame
    dontClear: true);
```

> If a GIF shows a black or colored background, call `GifLoadManager.Instance.PrepareGif(name, forceBgColorTransparent: true)` before it is displayed for the first time to make the background transparent.

## Editor tools

| Entry | Purpose |
| --- | --- |
| `Window > SS > GIF Viewer` | Preview the decoding result, frame count and size of a `.gif` or `.bytes` file |
| Importing a `.gif` | Converts it into `.bytes` for `Resources` loading |

> Verbose decode logging is off by default; set `GifDecoder.VerboseLog = true` while debugging.

## Known limitations

- Legacy `Text` only; TextMeshPro is not supported.
- Plain Text Extension blocks inside a GIF file are never drawn. They are metadata stored in the file (commonly copyright, author or description text), not text overlaid on the animation; the package parses and keeps them but does not render them.
- Icons and GIFs occupy one character cell each; oversized images are squeezed into that cell.
- GIF decoding runs in a coroutine, so very large GIFs have a noticeable first-display cost.
- Disabling `supportRichText` falls back to the native `Text` behavior and all extended tags stop working.
- The code uses C# 9 (`is not` patterns in 3 places, target-typed `new()` in 2 places), so Unity 2020.3 LTS (C# 8) cannot compile it as-is. Rewriting those 5 expressions in C# 8 syntax restores 2020.3 support.

## Project structure

```text
Assets/SSRichText/                 # Distributable UPM package
  Runtime/Scripts/                 # RichText, IconProvider, GIF decoding/loading
  Runtime/Shaders/                 # Shaders used to merge icon/GIF textures
  Editor/                          # GIF Viewer and import handling (separate editor assembly)
Assets/Scenes/SampleScene.unity    # Sample scene
Assets/Scripts/Sample/             # Sample scripts
Assets/Resources/Icons/            # Sample icons (loaded from a Resources subfolder)
Assets/Resources/Gifs/             # Sample GIFs (loaded from a Resources subfolder)
```

## Changelog and roadmap

- See [CHANGELOG.md](CHANGELOG.md) for version history
- Implemented: outline, shadow, underline, strikethrough, gradient, link, icon, GIF, click event

## Credits

- Implementation ideas from [ZUIRichText](https://github.com/172672672/UGUI-RichText)
- GIF decoding ideas from [UnityGif](https://github.com/SleepyLoser/UnityGif) and [this GIF format article](https://blog.csdn.net/wzy198852/article/details/17266507)
- Sample icons from Alibaba Iconfont: [Stickers](https://www.iconfont.cn/collections/detail?cid=50743), [Colorful plants](https://www.iconfont.cn/collections/detail?cid=50664)
- Sample GIFs come from the internet

## License

[MIT](LICENSE)
