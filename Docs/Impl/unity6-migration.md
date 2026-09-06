# DenEmo — Unity 6 移行調査

- 調査日: 2026-09-06
- 現行: Unity 2022.3.22f1 / Built-in RP
- 目標: Unity 6 (6000.0 LTS) / **BiRP 維持**
- 共通調査: [`../../../Docs/Impl/unity6-migration-overview.md`](../../../Docs/Impl/unity6-migration-overview.md)

## 判定

🔍 **要検証** — Unity 6 非対応の API は **0 件**。修正すべきコードはない。
ワークスペース最大規模のツールだが、移行リスクは NDMF 内部 API リフレクションと
UI Toolkit / フォントの見た目に限られる。

## 構成

| 項目 | 内容 |
|---|---|
| 規模 | C# 42 ファイル / 約 12,129 行（ワークスペース最大級） |
| asmdef | なし（プロジェクト既定アセンブリ） |
| エントリ | `MenuItem("dennokoworks/DenEmo")` |
| UI | **UI Toolkit**（`.uxml` 5 / `.uss` 2）+ IMGUI 併用 + SceneView |
| 外部依存 | **NDMF**（内部 API リフレクション）、**VRChat SDK**（リフレクション参照のみ） |

**VRChat SDK を直接型参照していない**点が重要。SDK 型は文字列＋リフレクションで解決するため、
**SDK 未導入 / SDK が Unity 6 未対応でもコンパイルが通り、単体検証を先行できる**。

## 検出事項

### 1. NDMF `PreviewSession` 内部 API へのリフレクション（🔍 要検証・最大のリスク）

`Editor/DenEmoWindow.VertexFilter.cs:226-250`

```csharp
_ndmfSessionType = assembly.GetType("nadena.dev.ndmf.preview.PreviewSession");
_ndmfCurrentProp = _ndmfSessionType.GetProperty("Current", ...);
_ndmfMapProp     = _ndmfSessionType.GetProperty("OriginalToProxyRenderer",
                       BindingFlags.NonPublic | BindingFlags.Instance);
_ndmfTryGetValue = ...;
```

- `PreviewSession.OriginalToProxyRenderer` は **NDMF の非 public プロパティ**。
  NDMF が Unity 6 対応で実装を変えると、コンパイルエラーにならず解決に失敗する。
- `_ndmfResolved` フラグで一度だけ解決を試み、失敗時は素のレンダラを使う経路へ縮退する。
  **例外は出ない。**
- Unity 6 で起きるのは「壊れる」ではなく「**頂点フィルタが NDMF プレビュー後の
  メッシュではなく元メッシュを見るようになり、他プラグインの改変が反映されなくなる**」劣化。

**リスクの性質**: Unity 6 固有ではなく **NDMF のバージョン追従の問題**。
Unity 6 移行と NDMF 更新が同時に起きるため、この移行タイミングで顕在化しやすい。

**対応**: 解決失敗時に一度だけ警告ログを出し、サイレント縮退を可視化する。

```csharp
if (_ndmfSessionType == null || _ndmfMapProp == null)
    Debug.LogWarning("[DenEmo] NDMF PreviewSession を解決できませんでした。頂点フィルタは元メッシュを参照します。");
```

### 2. VRChat SDK 型のリフレクション解決（✅ 影響なし・良い実装）

`Editor/Core/VrcAvatarReflection.cs:9-30`、`Editor/DenEmoWindow.cs:968-969`

```csharp
if (name == "VRC_AvatarDescriptor" || name == "VRCAvatarDescriptor" ||
    (full != null && (full.Contains("VRC_AvatarDescriptor") || full.Contains("VRCAvatarDescriptor"))))
```

- **VRChat SDK の型へ直接依存せず、コンポーネント型名の文字列一致で判定**している。
  旧 `VRC_AvatarDescriptor` にも対応済み。
- Unity 6 で SDK の名前空間やアセンブリ構成が変わっても、**型名が維持される限り動作する**。
- 型名ベースなので SDK のアセンブリ名変更にも耐える。**Unity 6 移行という観点では最も堅牢な実装。**

**修正不要。** ただし SDK 側が型名自体を変えた場合は追従が必要（可能性は低い）。

`Editor/Core/VrcBindingExclusionRule.cs` の Eyelids / LipSync バインド判定も同様にリフレクション経由。

### 3. UI Toolkit（🔍 視覚検証のみ）

UXML: `DenEmoWindow.uxml`, `DenEmoTimelineWindow.uxml`, `FxClipPickerPopup.uxml`,
`ShapeKeyList.uxml`, `ShapeKeyRow.uxml`, `VertexPreviewOptionsPopup.uxml`
USS: `DenEmoStyles.uss`, `DennokoTheme.uss`

- Unity 6 で Obsolete 化する `ExecuteDefaultAction` / `ExecuteDefaultActionAtTarget` /
  `PreventDefault` は **未使用**。
- Unity 6 で非推奨になる `UxmlFactory` / `UxmlTraits` も **未使用**
  （カスタム要素は C# で直接構築している）。
- UXML / USS のスキーマ変更はない。**コード修正不要。**

**対応**: Unity 6 の既定 USS 変更による見た目のずれのみ確認する。
特に `TimelineUITKView` / `ShapeKeyListUI` のような密度の高いリスト UI は行高・余白がずれやすい。

### 4. フォント生成（`UnityEngine.TextCore.Text.FontAsset`）（🔍 実機確認）

`Editor/Utils/DennokoUIFont.cs:5,145`

```csharp
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
...
var fa = FontAsset.CreateFontAsset(FamilyName, StyleName);   // OS のメイリオから SDF 生成
```

- `UnityEngine.TextCore.Text` は **ビルトインモジュール**であり `com.unity.textmeshpro`
  パッケージに依存しない。→ Unity 6 の TMP パッケージ統合の**影響を受けない**。
- API は Unity 6 でも存在する。
- ソース内コメント（`:18-19`）にあるとおり、fake-null キャッシュとアトラス leak の対策が
  既に施されている（`HideAndDontSave` 伝播、`IsAlive` チェック）。

**対応**: SDF アトラス生成は TextCore の内部実装に依存するため、
**Unity 6 で日本語が表示されるかの実機確認は必須**。

`Editor/Utils/DennokoUIFont.cs:130` の `Resources.FindObjectsOfTypeAll<FontAsset>()` は
**Obsolete ではない**（共通調査 2.2 節の注記参照）。修正不要。

### 5. `AnimationUtility`（✅ 影響なし）

`Editor/Core/AnimationClipEditor.cs`, `Editor/Core/AnimationExporter.cs` で
`GetEditorCurve` / `SetEditorCurve` / `SetEditorCurves` / `GetCurveBindings` /
`SetKeyLeftTangentMode` / `SetKeyRightTangentMode` / `AnimationUtility.TangentMode` を使用。

Unity 6 で `AnimationUtility` の API 変更は **なし**。**修正不要。**

### 6. バージョン定義（✅ 影響なし）

| ファイル | 定義 | Unity 6 での評価 |
|---|---|---|
| `Editor/Core/AnimationClipEditor.cs:406` | `#if UNITY_2022_1_OR_NEWER` | **true**（`SetEditorCurves` の一括版が使われる） |
| `Editor/Utils/DennokoVersionChecker.cs:133` | `#if UNITY_2020_2_OR_NEWER` | **true** |

いずれも新しい側の分岐が採用され、旧分岐が死にコードになるだけ。**動作は変わらない。修正不要。**

### 7. `SceneView.duringSceneGui`（✅ 影響なし）

`Editor/DenEmoWindow.cs:190,240` — 登録／解除が対称。Unity 6 で API 変更なし。

## 非該当の確認

| 確認項目 | 結果 |
|---|---|
| `Object.FindObjectsOfType` / `FindObjectOfType` | **なし**（`Resources.FindObjectsOfTypeAll` のみ = Obsolete 対象外） |
| UnityEditor（Unity 本体）内部 API へのリフレクション | **なし**（NDMF のみ） |
| `GraphicsFormat.DepthAuto` / `ShadowAuto` / `VideoAuto` | **なし** |
| UI Toolkit の Obsolete API | **なし** |
| IMGUI テーマ（共有 `EditorStyles` 書き換え） | **なし** |
| Compute シェーダ / カスタムシェーダ | **なし** |
| `Lightmapping` / 物理 API | **なし** |

## 移行手順

### フェーズ 1（Unity 2022.3.22f1 のまま実施可）

- [ ] `DenEmoWindow.VertexFilter.cs` の NDMF 解決失敗時に警告ログを追加
- [ ] `MenuItem("Tools/Your Tool Name")` というテンプレート由来の未整理メニュー項目の確認

### フェーズ 2（Unity 6 検証プロジェクト・先行実施可）

**VRChat SDK を直接型参照していないため、SDK の Unity 6 対応を待たずに検証を開始できる。**
Unity 6 の空プロジェクトに `DenEmo/` をコピーして検証する。
（NDMF プレビュー連携のみ NDMF 対応版が必要。それ以外の機能は SDK/NDMF なしで確認できる。）

- [ ] コンパイルエラー・警告が 0 件
- [ ] `dennokoworks/DenEmo` からウィンドウが開く

## 検証チェックリスト（Unity 6）

### SDK/NDMF なしで確認できる項目（先行検証）

- [ ] ウィンドウが開き、**日本語が正しく表示される**（TextCore フォント生成の確認）
- [ ] UI Toolkit のレイアウト（シェイプキーリスト、タイムライン）が崩れていない
- [ ] シェイプキー一覧の取得・チェック操作
- [ ] アニメーションクリップの読み込み・カーブ編集・書き出し（`AnimationUtility` 経路）
- [ ] タンジェントモード（Constant / Linear / ClampedAuto）の切り替えが反映される
- [ ] FX セットアップモード / アニメーション補正モードの UI
- [ ] SceneView 上の頂点プレビュー描画
- [ ] タイムラインウィンドウの表示・スクラブ

### SDK/NDMF 対応版で確認する項目

- [ ] `VRCAvatarDescriptor` の自動検出（リフレクション経路）が機能する
- [ ] Eyelids / LipSync にバインドされたシェイプキーが除外対象としてマークされる
- [ ] **頂点フィルタが NDMF プレビュー結果を参照している**（元メッシュに落ちていない）
      — 追加した警告ログが出ていないことで確認
