# 发布流程

面向维护者。用户的安装方式见 [README](README.md)。

版本号遵循[语义化版本](https://semver.org/lang/zh-CN/)：修复 → patch，新功能 → minor，不兼容变更 → major。

## 1. 准备代码

- 完成代码与文档修改，确认工程能正常编译。
- 如果是 Editor 相关改动，打开一次 Unity 确认 `Window > SS > GIF Viewer` 等入口正常。

## 2. 同步版本号与变更记录

- 修改 `Assets/SSRichText/package.json` 的 `version`，例如 `2.1.0`。
- 修改 `CHANGELOG.md`：
  - 把 `Unreleased` 下的内容移到新的 `## [x.y.z] - yyyy-MM-dd` 小节；
  - 保留空的 `## [Unreleased]`；
  - 更新底部的 `[Unreleased]` 与 `[x.y.z]` 对比链接。
- 更新 `README.md` 与 `Assets/SSRichText/README.md` 中固定版本的示例（`#vX.Y.Z`）。

## 3. 提交并打 tag

```bash
git add -A
git commit -m "feat: ..."
git tag vX.Y.Z
git push origin main vX.Y.Z
```

> tag 必须指向包含本次版本号与 CHANGELOG 的提交，否则 UPM 用 `#vX.Y.Z` 固定的内容会和 Release 说明对不上。

## 4. 打包 unitypackage

```powershell
powershell -ExecutionPolicy Bypass -File Tools~/BuildUnityPackage.ps1
```

产物默认是 `dist/SSRichText.unitypackage`（`dist/` 已被忽略，不会进版本库）。

脚本按 Unity 的 `.unitypackage` 格式打包：每个资源对应一个以 GUID 命名的目录，包含 `asset`、`asset.meta`、`pathname`；文件夹条目只包含 `asset.meta` 与 `pathname`，以保证导入后 GUID 不变。

## 5. 创建 Release 并上传附件

- 在 GitHub 上基于 `vX.Y.Z` 创建 Release，标题使用 `VX.Y.Z`。
- 说明内容直接使用 `CHANGELOG.md` 中对应版本的小节。
- 上传 `dist/SSRichText.unitypackage`，文件名保持 `SSRichText.unitypackage`。

## 6. 验证

- Release 页面能下载到附件；
- 下载后能在 Unity 中通过 `.unitypackage` 导入；
- UPM 使用 `https://github.com/SlipperSoar/SSRichText.git?path=/Assets/SSRichText#vX.Y.Z` 能正常解析到该版本。
