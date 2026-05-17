# UnityEasyWorkTools - TableImporter

TableImporter 是 UnityEasyWorkTools 的导表模块, 用于把 `.xlsx` 和 `.csv` 表格导入为 ScriptableObject 资产。

它适合把物品表、武器表、掉落表、关卡配置表等编辑期数据转换为 Unity 运行时可以直接读取的强类型资产。

## 项目特点

- 支持从 `.xlsx` 和 `.csv` 表格导入数据。
- 通过少量 C# importer 代码，把表格列映射到 ScriptableObject 字段。
- 支持字符串、整数、浮点数、布尔值、向量、颜色、枚举、Unity 资产引用和列表等常见类型。
- 提供 Unity 编辑器窗口，支持导入、导出和代码生成流程。
- 通用导表核心和项目专用 importer 分开编译, 便于后续替换或扩展。
- 作为 UnityEasyWorkTools 的一部分放在 `Assets/UnityEasyWorkTools/TableImporter` 下。

## 环境要求

- Unity 2022.3 或更高版本。
- 仅在 Unity Editor 中使用。

## 安装方式

把 `Assets/UnityEasyWorkTools` 目录放入项目即可使用。

## 快速开始

1. 确认 `Assets/UnityEasyWorkTools/TableImporter` 已在项目中。
2. 通过 `Tools/UnityEasyWorkTools/Settings/Open Path Settings` 调整默认路径。
3. 在 `Importers` 目录中创建或维护项目 importer 脚本。
4. 继承 `Excel2SoListAssetImporter<TAsset>` 或 `Excel2SoRowAssetImporter<TAsset>`。
5. 在 `Configure(Excel2SoMapping map)` 中配置列到字段的映射。
6. 在 Unity 菜单打开 `Tools/UnityEasyWorkTools/Table Importer/Importer Window`。
7. 选择表格文件和目标资产后执行导入。

## 说明书

详细用法、importer 示例、asmdef 配置和常见问题见 [Docs/说明书.md](Docs/%E8%AF%B4%E6%98%8E%E4%B9%A6.md)。

## 目录结构

```text
Assets/UnityEasyWorkTools/TableImporter/
Editor/
  Core/       表格读取、导入流程、字段映射、代码生成。
  UI/         Unity 编辑器窗口、UXML 和 USS。
Importers/    项目专用导入器。
Docs/
  说明书.md   使用说明书。
```
