# DenEmo

## 概要

DenEmoは、Unity上でSkinnedMeshRendererのシェイプキー（ブレンドシェイプ）を効率的に調整・アニメーション化するためのエディタ拡張ツールです。VRChat向けモデルや表情アニメーションの作成をサポートします。

- シェイプキー値の調整
- アニメーションファイル（.anim）の生成
- 既存アニメーションとのキー揃え
- スナップショット保存・復元
- Undo/Redo対応
- 日本語・英語UI切り替え
- 左右同期編集（Symmetry）モード（L/R統合表示・同時編集）

## 使い方

1. Unityのメニュー「Tools > DenEmo」からウィンドウを開きます。
2. 対象の顔のメッシュ（SkinnedMeshRendererを持つオブジェクト）を指定します。
3. シェイプキー値を調整し、必要に応じて保存対象を選択します。
4. 「アニメーションを保存」ボタンで.animファイルを出力できます。
5. 英語UIに切り替えたい場合は、画面右上の「英語モード」チェックをONにしてください。

### 左右同期編集（Symmetry）
- フィルタ行の「Symmetry」チェックをONにすると、末尾が L/R のシェイプキーを1行に統合して表示します。
- 対応サフィックス: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R`（小文字の`l`/`r`も可）; `_左`/`_右`, `.左`/`.右`, `-左`/`-右`, ` (左)`/` (右)`, ` 左`/` 右`
- 統合行でのスライダー操作・0ボタン・チェックは左右に同じ値で適用されます。
- 片側しか存在しない場合は通常通り単独で表示されます。

## ファイル構成
- Shapekey_animation_converter.cs: メインウィンドウ
- Shapekey_animation_converter.UI.cs: UIロジック
- Shapekey_animation_converter.State.cs: 状態管理
- Shapekey_animation_converter.Animation.cs: アニメーション保存/適用
- DenEmoLocalization.cs: 多言語リソース

---

# DenEmo

## Overview

DenEmo is a Unity Editor extension for efficiently editing and animating blendshapes (shape keys) on SkinnedMeshRenderer components. Ideal for VRChat avatars and facial animation workflows.

- Batch and individual blendshape adjustment
- Animation file (.anim) export
- Key alignment with existing animations
- Snapshot save/restore
- Undo/Redo support
- Switchable Japanese/English UI
- Symmetry edit mode (merge and edit L/R together)

## Usage

1. Open the window from Unity menu: "Tools > DenEmo"
2. Assign your target SkinnedMeshRenderer
3. Adjust blendshape values and select which shapes to include
4. Click "Save Animation" to export a .anim file
5. To switch to English UI, enable "English Mode" at the top right

### Symmetry Edit
- Turn on the "Symmetry" toggle in the filter row to merge shape keys that end with L/R into a single row.
- Supported suffixes: `_L`/`_R`, `.L`/`.R`, `-L`/`-R`, ` (L)`/` (R)`, ` L`/` R` (lowercase `l`/`r` also supported); `_左`/`_右`, `.左`/`.右`, `-左`/`-右`, ` (左)`/` (右)`, ` 左`/` 右`
- Slider, zero button, and include checkbox apply to both sides simultaneously.
- If only one side exists, it is shown as a normal single row.

### Notes
- While dragging sliders, mesh updates are throttled (~50 ms) for better Editor performance; the final value is applied when you release the mouse.
- Shape keys used for VRChat visemes (lip-sync) are automatically excluded from editing/saving based on the Avatar Descriptor assignments.

## File Structure
- Shapekey_animation_converter.cs: Main window
- Shapekey_animation_converter.UI.cs: UI logic
- Shapekey_animation_converter.State.cs: State management
- Shapekey_animation_converter.Animation.cs: Animation save/apply
- DenEmoLocalization.cs: Localization resources

## License
MIT License
