# TableImporter

TableImporter 是 UnityEasyWorkTools 的导表模块, 用于把 `.xlsx` 和 `.csv` 表格导入为 ScriptableObject 资产。

项目地址: [AChen1111/UnityEasyWorkTools](https://github.com/AChen1111/UnityEasyWorkTools)

## 目录

- [项目链接](#项目链接)
- [功能](#功能)
- [安装](#安装)
- [快速开始](#快速开始)
- [详细说明](#详细说明)
- [目录结构](#目录结构)

## 项目链接

- [GitHub 仓库](https://github.com/AChen1111/UnityEasyWorkTools)
- [主 README](../README.md)
- [Issues](https://github.com/AChen1111/UnityEasyWorkTools/issues)

## 功能

- 读取 `.xlsx` 和 `.csv` 表格。
- 通过 C# importer 把表格列映射到 ScriptableObject 字段。
- 支持字符串、整数、浮点数、布尔值、向量、颜色、枚举、Unity 资产引用和列表。
- 提供 Unity 编辑器窗口, 支持导入、导出和代码生成。
- 插件只保留通用导表核心, 具体项目导入器由使用者在自己的项目目录中维护。

## 安装

把仓库放入 Unity 项目的以下目录:

```text
Assets/UnityEasyWorkTools
```

导入后打开路径配置:

```text
Tools/UnityEasyWorkTools/Settings/Open Path Settings
```

## 快速开始

1. 在项目自己的 Editor 目录中创建 importer 脚本。
2. 继承 `Excel2SoListAssetImporter<TAsset>` 或 `Excel2SoRowAssetImporter<TAsset>`。
3. 在 `Configure(Excel2SoMapping map)` 中配置列到字段的映射。
4. 打开 `Tools/UnityEasyWorkTools/Table Importer/Importer Window`。
5. 选择 importer、表格文件和目标资产后执行导入。

## 详细说明

- [返回主 README](../README.md)
- [TableImporter 说明书](Docs/%E8%AF%B4%E6%98%8E%E4%B9%A6.md)

## 目录结构

```text
TableImporter/
  Editor/Core/  表格读取、导入流程、字段映射、代码生成.
  Editor/UI/    Unity 编辑器窗口、UXML 和 USS.
  Docs/         使用说明.
```
