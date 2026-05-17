---
name: unity-easy-work-tools-context
description: Understand and maintain the UnityEasyWorkTools standalone Unity editor plugin, including module boundaries, dependencies, folder layout, and clean-plugin rules.
---

# UnityEasyWorkTools Context

## Project Summary

UnityEasyWorkTools is a standalone Unity editor tooling suite. The repository root is intended to be copied or mounted into a Unity project at:

```text
Assets/UnityEasyWorkTools
```

The plugin currently contains:

- `AnimationSequence`: DOTween-based visual animation sequence assets, runtime player, and editor window.
- `UIAutoBind`: UI component auto-binding and generated partial class helpers.
- `TableImporter`: generic Excel/CSV to ScriptableObject editor tooling.
- `Shared`: shared editor settings and path configuration.

`AnimationSequence` currently keeps the legacy `Game.Animation` namespace and `Game.AnimationSequence` asmdef name for compatibility with existing users. Do not add references to game-specific assemblies from this repository.

## Core Rules

- Keep this repository clean and reusable. Do not add P_GUN-specific gameplay, item, buff, weapon, enemy, database, or Addressables importer code to the standalone plugin.
- Project-specific table importers should live in the consuming Unity project, or in a clearly separated sample package, not in this clean plugin root.
- Keep Unity `.meta` files when adding or moving Unity assets, because users may install this repository directly under `Assets/UnityEasyWorkTools`.
- Editor UI should use UI Toolkit with `.uxml` and `.uss` files in each module's `Editor/UI` folder.
- Do not put animation runtime behavior inside editor windows. Runtime animation creation belongs to `AnimationTweenFactory`.
- Do not introduce broad fallback behavior that hides missing configuration. Prefer clear editor errors or explicit configuration through `UnityEasyWorkToolsPathSettings.asset`.
- Write necessary code comments in Chinese descriptions with English punctuation.

## Module Layout

```text
AnimationSequence/
  Scripts/      Runtime animation sequence asset, step data, player, and tween factory.
  Editor/       Animation sequence editor window.
  Editor/UI/    UXML and USS for the editor window.

UIAutoBind/
  Scripts/      ComponentAutoBindTool and binding rule APIs.
  Editor/       Inspectors and code generation.
  Editor/UI/    UXML and USS for inspectors.

TableImporter/
  Editor/Core/  Generic table reading, mapping, import/export, and code generation.
  Editor/UI/    Importer window UI Toolkit assets and window script.
  Docs/         User-facing documentation.

Shared/
  Editor/       UnityEasyWorkToolsPathSettings and custom inspector.
  Editor/UI/    Settings inspector UXML and USS.
```

## Dependencies

- Unity 2022.3 or newer.
- DOTween is required by `AnimationSequence`.
- `TableImporter` core should not depend on any game project assemblies.
- Addressables support should stay outside the clean core unless it is made optional and isolated from projects without Addressables installed.

## Public Entry Points

- `Tools/UnityEasyWorkTools/Animation Sequence Editor`
- `Tools/UnityEasyWorkTools/Table Importer/Importer Window`
- `Tools/UnityEasyWorkTools/Settings/Open Path Settings`
- `Tools/UnityEasyWorkTools/UI Auto Bind/Create Global Setting`

## Table Importer Guidance

The clean plugin ships only the generic importer framework. A consuming project creates its own importer scripts that inherit from:

```csharp
Excel2SoListAssetImporter<TAsset>
Excel2SoRowAssetImporter<TAsset>
ExcelTableImporterBase
```

If a project uses asmdef files for its custom importers, that project-owned importer assembly should reference:

```text
Excel2SO.Editor
```

and any project runtime assemblies that define its target ScriptableObject types.

## Validation

After changes:

- Search for accidental project-specific dependencies with `rg "P_GUN|Game.Core|Game.Items|Game.Gameplay|Unity.Addressables|AddressablesLabel"`.
- Check that editor UI code does not reintroduce IMGUI-only layout for plugin inspectors/windows.
- If inside a Unity project, refresh assets and verify the Unity console has no compile errors.
