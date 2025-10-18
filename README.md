# Shapekey Animation Converter

## 概要

Shapekey Animation Converterは、Unity上でSkinnedMeshRendererのシェイプキー（ブレンドシェイプ）を効率的に調整・アニメーション化するためのエディタ拡張ツールです。VRChat向けモデルや表情アニメーションの作成をサポートします。

- シェイプキー値の調整
- アニメーションファイル（.anim）の生成
- 既存アニメーションとのキー揃え
- スナップショット保存・復元
- Undo/Redo対応
- 日本語・英語UI切り替え

## 使い方

1. Unityのメニュー「Tools > DenEmo」からウィンドウを開きます。
2. 対象の顔のメッシュ（SkinnedMeshRendererを持つオブジェクト）を指定します。
3. シェイプキー値を調整し、必要に応じて保存対象を選択します。
4. 「アニメーションを保存」ボタンで.animファイルを出力できます。
5. 英語UIに切り替えたい場合は、画面右上の「英語モード」チェックをONにしてください。

## ファイル構成
- Shapekey_animation_converter.cs: メインウィンドウ
- Shapekey_animation_converter.UI.cs: UIロジック
- Shapekey_animation_converter.State.cs: 状態管理
- Shapekey_animation_converter.Animation.cs: アニメーション保存/適用
- DenEmoLocalization.cs: 多言語リソース

---

# Shapekey Animation Converter

## Overview

Shapekey Animation Converter is a Unity Editor extension for efficiently editing and animating blendshapes (shape keys) on SkinnedMeshRenderer components. Ideal for VRChat avatars and facial animation workflows.

- Batch and individual blendshape adjustment
- Animation file (.anim) export
- Key alignment with existing animations
- Snapshot save/restore
- Undo/Redo support
- Switchable Japanese/English UI

## Usage

1. Open the window from Unity menu: "Tools > DenEmo"
2. Assign your target SkinnedMeshRenderer
3. Adjust blendshape values and select which shapes to include
4. Click "Save Animation" to export a .anim file
5. To switch to English UI, enable "English Mode" at the top right

## File Structure
- Shapekey_animation_converter.cs: Main window
- Shapekey_animation_converter.UI.cs: UI logic
- Shapekey_animation_converter.State.cs: State management
- Shapekey_animation_converter.Animation.cs: Animation save/apply
- DenEmoLocalization.cs: Localization resources

## License
MIT License
