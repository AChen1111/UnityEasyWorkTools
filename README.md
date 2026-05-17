# UnityEasyWorkTools

UnityEasyWorkTools 是面向 Unity 项目的编辑器效率工具集合, 当前包含动画序列编辑、UI 自动绑定和表格导入三个模块。工具统一放在 `Tools/UnityEasyWorkTools` 菜单下, 并通过路径配置资产管理默认目录。

项目地址: [AChen1111/UnityEasyWorkTools](https://github.com/AChen1111/UnityEasyWorkTools)

## 目录

- [项目链接](#项目链接)
- [功能模块](#功能模块)
- [安装](#安装)
- [使用](#使用)
- [环境要求](#环境要求)
- [目录结构](#目录结构)
- [发布前检查](#发布前检查)

## 项目链接

- [GitHub 仓库](https://github.com/AChen1111/UnityEasyWorkTools)
- [Issues](https://github.com/AChen1111/UnityEasyWorkTools/issues)

## 功能模块

### AnimationSequence

可视化 DOTween 动画序列工具。

- 使用 `AnimationSequenceAsset` 保存动画步骤。
- 使用 `AnimationPlayer` 在运行时播放动画资产。
- 支持 `FadeIn`、`FadeOut`、`SlideUp`、`Shake`、`ScaleIn`、`ScaleOut`、`MoveTo`、`Rotate`。
- 支持目标启动激活状态、播放时自动激活、播放完成事件和可选还原状态。

菜单入口:

```text
Tools/UnityEasyWorkTools/Animation Sequence Editor
```

### UIAutoBind

UI 组件自动绑定和代码生成工具。

- 在 UI 根节点挂载 `ComponentAutoBindTool`。
- 按绑定规则扫描子节点组件。
- 支持排序、清空、移除空引用、自动绑定、生成绑定代码和自动挂载生成脚本。

菜单入口:

```text
Tools/UnityEasyWorkTools/UI Auto Bind/Create Global Setting
```

### TableImporter

Excel/CSV 到 ScriptableObject 的导表工具。

- 支持 `.xlsx` 和 `.csv` 表格读取。
- 支持字段映射、导入、导出和代码生成。
- 通用核心位于 `TableImporter/Editor/Core`。
- 具体项目导入器由使用者在自己的项目目录中创建, 插件仓库不内置业务示例。

菜单入口:

```text
Tools/UnityEasyWorkTools/Table Importer/Importer Window
```

文档:

- [TableImporter README](TableImporter/README.md)
- [TableImporter 说明书](TableImporter/Docs/%E8%AF%B4%E6%98%8E%E4%B9%A6.md)

### Shared Settings

插件共用路径配置使用 `UnityEasyWorkToolsPathSettings.asset` 管理。

菜单入口:

```text
Tools/UnityEasyWorkTools/Settings/Open Path Settings
```

## 安装

推荐把仓库放到 Unity 项目的以下目录:

```text
Assets/UnityEasyWorkTools
```

也可以通过 Git submodule 安装:

```powershell
git submodule add https://github.com/AChen1111/UnityEasyWorkTools.git Assets/UnityEasyWorkTools
```

导入后先打开路径配置:

```text
Tools/UnityEasyWorkTools/Settings/Open Path Settings
```

## 使用

1. 打开 [路径配置](#shared-settings), 按项目目录调整生成代码、生成资产、CSV 和表资产路径。
2. 需要编辑动画序列时, 打开 [AnimationSequence](#animationsequence) 菜单入口。
3. 需要生成 UI 绑定代码时, 先创建 [UIAutoBind](#uiautobind) 全局配置, 再在目标 UI 根节点挂载 `ComponentAutoBindTool`。
4. 需要导入表格时, 打开 [TableImporter](#tableimporter) 窗口, 详细流程见 [TableImporter 说明书](TableImporter/Docs/%E8%AF%B4%E6%98%8E%E4%B9%A6.md)。

## 环境要求

- Unity 2022.3 或更高版本。
- DOTween, 用于 `AnimationSequence` 运行时动画。

## 目录结构

```text
UnityEasyWorkTools/
  AnimationSequence/
    Scripts/      动画序列运行时代码.
    Editor/       动画序列编辑器代码.
  Shared/
    Editor/       共用路径配置和 Inspector.
  UIAutoBind/
    Scripts/      UI 自动绑定运行时组件和规则.
    Editor/       UI 自动绑定 Inspector 和代码生成.
  TableImporter/
    Editor/Core/  表格读取、字段映射、导入、导出、代码生成核心.
    Editor/UI/    导表窗口 UXML、USS 和窗口脚本.
    Docs/         导表工具说明.
```

## 发布前检查

- 不要在 `Assets/UnityEasyWorkTools` 内创建嵌套 `.git` 仓库。
- 不要把具体项目的表导入器、业务程序集引用或 Addressables 项目脚本提交到干净插件仓库。
- 当前默认路径以 `Assets/UnityEasyWorkTools` 为根, 改为 UPM Git package 前需要同步调整路径配置。
