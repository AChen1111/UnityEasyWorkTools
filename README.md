# UnityEasyWorkTools

UnityEasyWorkTools 是一组面向 Unity 项目的编辑器效率工具集合, 当前包含动画序列编辑、UI 自动绑定和表格导入三个模块。插件目标是把常用的编辑期流程收拢到 `Tools/UnityEasyWorkTools` 菜单下, 并通过可配置的路径资产降低项目迁移成本。

## 功能模块

### AnimationSequence

可视化 DOTween 动画序列工具。

- 使用 `AnimationSequenceAsset` 保存动画步骤。
- 使用 `AnimationPlayer` 在运行时播放动画资产。
- 支持 FadeIn、FadeOut、SlideUp、Shake、ScaleIn、ScaleOut、MoveTo、Rotate 等硬编码效果。
- 支持目标启动激活状态、播放时自动激活、播放完成事件和可选还原状态。
- 编辑器窗口使用 UI Toolkit, 界面资源位于 `AnimationSequence/Editor/UI`。

菜单入口:

```text
Tools/UnityEasyWorkTools/Animation Sequence Editor
```

### UIAutoBind

UI 组件自动绑定和代码生成工具。

- 在目标 UI 根节点上挂载 `ComponentAutoBindTool`。
- 根据绑定规则扫描子节点 UI 组件。
- 支持排序、清空、移除空引用、自动绑定、生成绑定代码和自动挂载生成脚本。
- 全局配置资产位于 `UIAutoBind/AutoBindGlobalSetting.asset`。
- 自定义 Inspector 使用 UI Toolkit, 界面资源位于 `UIAutoBind/Editor/UI`。

### TableImporter

Excel/CSV 到 ScriptableObject 的导表工具。

- 提供通用表格读取、字段映射、导入、导出和代码生成流程。
- 通用核心位于 `TableImporter/Editor/Core`。
- 编辑器窗口和样式位于 `TableImporter/Editor/UI`。
- `TableImporter/Importers` 中当前放有 P_GUN 项目的业务导入器示例, 依赖 `Game.Items`、`Game.Gameplay` 等项目程序集。

菜单入口:

```text
Tools/UnityEasyWorkTools/Table Importer/Importer Window
Tools/UnityEasyWorkTools/Addressables/Create Labels CSV Template
Tools/UnityEasyWorkTools/Addressables/Import Labels From Excel
```

详细说明见:

```text
TableImporter/README.md
TableImporter/Docs/说明书.md
```

### Shared Settings

插件共用路径配置使用 `UnityEasyWorkToolsPathSettings.asset` 管理。

菜单入口:

```text
Tools/UnityEasyWorkTools/Settings/Open Path Settings
```

配置 Inspector 支持选择路径、定位资源和在系统文件管理器中打开目录。

## 环境要求

- Unity 2022.3 或更高版本。
- DOTween, 用于 `AnimationSequence` 运行时动画。
- Unity Addressables, 仅 `TableImporter/Importers/AddressablesLabelTableImporter` 需要。
- 如果保留 `TableImporter/Importers` 中的 P_GUN 业务导入器, 项目需要提供对应的 `Game.Core`、`Game.Items`、`Game.Gameplay`、`Game.Animation` 程序集。

## 安装方式

推荐把仓库内容放到 Unity 项目的以下位置:

```text
Assets/UnityEasyWorkTools
```

也可以用 Git subtree 或 submodule 管理:

```powershell
git subtree add --prefix=Assets/UnityEasyWorkTools https://github.com/<owner>/UnityEasyWorkTools.git main --squash
```

导入后在 Unity 菜单打开:

```text
Tools/UnityEasyWorkTools/Settings/Open Path Settings
```

根据项目实际目录调整生成代码、生成资产、CSV、表资产等路径。

## 独立发布注意事项

当前仓库按 P_GUN 项目内的整体工具形态整理。若要发布为完全通用插件, 建议进一步拆分:

- 保留 `AnimationSequence`、`UIAutoBind`、`TableImporter/Editor`、`Shared` 作为通用工具。
- 将 `TableImporter/Importers` 中依赖业务程序集的导入器移动到项目自己的目录, 或作为示例代码单独放置。
- 如果想作为 UPM Git package 安装, 需要再调整默认路径配置, 因为当前默认路径以 `Assets/UnityEasyWorkTools` 为根。

## 单独上传到 GitHub

本项目内不要在 `Assets/UnityEasyWorkTools` 里创建嵌套 `.git` 仓库。推荐先导出到项目外的独立目录, 再推送到 GitHub。

示例流程:

```powershell
cd D:\GameWorkplace\Doing\UnityEasyWorkTools
git remote add origin https://github.com/<owner>/UnityEasyWorkTools.git
git push -u origin main
```

如果 GitHub 仓库还不存在, 先在 GitHub 网站创建空仓库, 或安装并登录 GitHub CLI 后执行:

```powershell
gh repo create <owner>/UnityEasyWorkTools --public --source . --remote origin --push
```

## 目录结构

```text
UnityEasyWorkTools/
  AnimationSequence/
    Scripts/      动画序列运行时代码.
    Editor/       动画序列编辑器代码.
    Editor/UI/    动画序列 UXML 和 USS.
  Shared/
    Editor/       共用路径配置和 Inspector.
    Editor/UI/    共用配置 Inspector 的 UXML 和 USS.
  UIAutoBind/
    Scripts/      UI 自动绑定运行时组件和规则.
    Editor/       UI 自动绑定 Inspector 和代码生成.
    Editor/UI/    UI 自动绑定 UXML 和 USS.
  TableImporter/
    Editor/Core/  表格读取、映射、导入、导出、代码生成核心.
    Editor/UI/    导表窗口 UXML、USS 和窗口脚本.
    Importers/    当前项目导入器示例.
    Docs/         导表工具说明书.
```

