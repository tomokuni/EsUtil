# EsUtil.Feature.Marquee.ControlMewUI サンプルアプリケーション

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 実行方法](#2-実行方法)
  - [2.1 バッチファイル（リリースビルド＆起動）](#21-バッチファイルリリースビルド起動)
- [3. 操作方法](#3-操作方法)
- [4. 検証項目（動作確認チェックリスト）](#4-検証項目動作確認チェックリスト)
- [5. 構成](#5-構成)
- [6. 使い方（初めての方向け）](#6-使い方初めての方向け)
- [7. 拡張例（計画の差し替え）](#7-拡張例計画の差し替え)
- [8. Avalonia 版サンプルとの違い](#8-avalonia-版サンプルとの違い)

---

## 1. 概要

`EsUtil.Feature.Marquee.ControlMewUI/sample/` にある **MarqueeControl（MewUI 版）の動作確認用の独立したサンプルアプリケーション**です。

- `MarqueeControl`（`EsUtil.Feature.Marquee.MewUI`）を背景画像の上へ重ねて動作を確認できます。
- ドラッグで選択矩形を作成・移動・リサイズし、nudge（矢印キー）で 1 ピクセル単位の微調整ができます。
- ズーム（1〜4 倍）は座標変換ポート（`CoordTransform.ClientToSource`）が担うため、選択矩形は常に元画像ピクセル座標で扱われます。
- 外観（枠線・ハンドル・マーカー・暗転マスク）と寸法・対話パラメータをスライダー/コンボボックスで変更できます。
- 選択の開始・変更・完了・キャンセル・確定要求・キャンセル要求・ルーペ位置のイベントと、コントロール内部のログを画面で確認できます。
- 見本として、外観と描画計画を差し替えた見た目（外枠の破線＋三分割のガイド線）を切り替えられます。

## 2. 実行方法

```powershell
dotnet run --project src/EsUtil.Feature.Marquee.ControlMewUI/sample/EsUtil.Feature.Marquee.ControlMewUI.Sample.csproj
```

リリース構成で実行する場合:

```powershell
dotnet run -c Release --project src/EsUtil.Feature.Marquee.ControlMewUI/sample/EsUtil.Feature.Marquee.ControlMewUI.Sample.csproj
```

### 2.1 バッチファイル（リリースビルド＆起動）

`src/EsUtil.Feature.Marquee.ControlMewUI/sample/RunSample.bat` をダブルクリックすると、リリースビルドを行った後、サンプルアプリケーションを起動します。

```text
RunSample.bat  →  リリースビルド（dotnet build -c Release）→ EsUtil.Feature.Marquee.ControlMewUI.Sample.exe を起動
```

> 本サンプルは独立したプロジェクトです。
> コントロール本体の `EsUtil.Feature.Marquee.ControlMewUI.csproj` は `sample/` をコンパイル対象に含めません（`sample/` は本体のプロジェクト フォルダの外にあるため）。

## 3. 操作方法

| 操作 | 内容 |
| --- | --- |
| 左ドラッグ | 新規選択（ハンドルの上からはリサイズ・移動） |
| 矢印キー | nudge（1 対象ピクセル単位の微調整。 クリックでフォーカスを与えてから使う） |
| Enter / Escape | 確定要求 / キャンセル要求（イベントとログで確認できる） |
| ズームのスライダー / マウス ホイール | 表示倍率（0.1〜5.0 倍。 ホイールは 0.1 刻み）。 選択矩形は元画像ピクセル座標のまま |
| 「画像内にクリップ」チェック | `Options.ClampToSourceBounds`（OFF の場合は画像外も選択できる） |
| 外観のコンボボックス | 枠線・ハンドル・マーカーの色（「なし」で描画なし） |
| 枠線の太さのスライダー | `Appearance.OuterBorderThickness`（0.5〜8 DIP） |
| 「暗転マスクを表示」チェック | `Appearance.ShowOverlay`（選択矩形の外側を暗転する） |
| 寸法・対話パラメータのスライダー | `Options`（ハンドル サイズ・枠の太さ・マーカーの長さ・ヒット半径・最小選択サイズ） |
| 「選択をリセット」/「矩形を設定」ボタン | `ResetSelection()` / `SetSelection(new EsRect(250, 180, 300, 200))` |
| 「確定を要求」/「キャンセルを要求」ボタン | `RequestConfirm()` / `RequestCancel()`（選択がある場合だけ確定要求が発火する） |
| 「外枠を破線…」チェック | 見本の外観と描画計画の適用（枠線の代わりに破線、内側に三分割のガイド線） |

## 4. 検証項目（動作確認チェックリスト）

- [ ] 背景画像の上をドラッグすると選択矩形（枠線・ハンドル・サイズラベル）が表示される
- [ ] ハンドルのドラッグでリサイズ、内側のドラッグで移動できる
- [ ] 最小選択サイズ未満のドラッグはキャンセル扱いになる（ログに「キャンセル」が出る）
- [ ] ズーム（スライダー・ホイール）を変えても選択矩形は元画像ピクセル座標のまま（サイズラベルの値が変わらない）
- [ ] 「画像内にクリップ」を OFF にすると画像外まで選択できる（ON では画像内に制限される）
- [ ] 外観（色・枠線の太さ・ハンドル・マーカー・暗転マスク）の変更が即時反映される
- [ ] 寸法・対話パラメータの変更が即時反映される（ハンドルの大きさ・ヒット半径・マーカーの長さ）
- [ ] 「矩形を設定」で選択矩形が復元され、「選択をリセット」で解除される
- [ ] 「確定を要求」で `ConfirmRequested` が発火する（選択がある場合のみ）
- [ ] 「キャンセルを要求」で `CancelRequested` が発火する
- [ ] クリックでフォーカスを与えた後、矢印キーで 1 ピクセル単位の微調整ができる（十字マーカーが出る）
- [ ] 選択中はログに `ルーペ位置:` が出る（`LoupePositionMoved`）
- [ ] 見本のチェックで外枠が破線、内側が三分割のガイド線になる（アンバー）
- [ ] 選択状態の欄とログ欄がイベントどおりに更新される

## 5. 構成

```text
EsUtil.Feature.Marquee.ControlMewUI/sample/
├── EsUtil.Feature.Marquee.ControlMewUI.Sample.csproj  # サンプルアプリのプロジェクト（net10.0-windows / MewUI 0.21.1）
├── RunSample.bat                # リリースビルド＆起動
├── Program.cs                   # エントリポイント（バックエンドとプラットフォーム ホストの登録）
├── Imaging/
│   └── TestImageFactory.cs      # 背景画像生成（グラデーション＋グリッド＋マーカー）
├── Logging/
│   └── InMemoryLogger.cs        # 画面のログ欄へ出力する ILogger
├── Presentation/
│   └── GuideMarqueePlan.cs      # 外観と描画計画の差し替え（見本）
└── Views/
    └── MainWindow.cs            # メイン ウィンドウ（設定パネル＋背景画像＋ドラッグ選択）
```

## 6. 使い方（初めての方向け）

1. **起動**: 背景画像（グラデーション＋グリッド＋マーカー）が表示されます。
2. **選択**: 画像の上を左ドラッグすると選択矩形が作られます（枠線・ハンドル・サイズラベルが出ます）。
3. **調整**: ハンドルのドラッグでリサイズ、内側のドラッグで移動、クリック後に矢印キーで微調整します。
4. **ズーム**: ズームを変えて、選択矩形が元画像ピクセル座標のまま保たれることを確認します。
5. **確定・キャンセル**: ボタンまたは Enter / Escape で要求を発火し、イベントとログを確認します。

## 7. 拡張例（計画の差し替え）

見本のチェックは、提示層を派生せずに公開 API だけで見た目を差し替えています。

- 外観: `MarqueeAppearance` の `with` 式で色・表示可否を変更する（`Appearance` へ設定）。
- 描画計画: `DrawPlanResolver` へ関数を設定し、外枠・内側の枠線の矩形の列を差し替える（`GuideMarqueePlan.Resolve`）。

計画の矩形は DIP で書けるため（`MarqueeDrawPlan.FocusDipRect` と `*DipRects`）、DPI が変わっても見た目は DIP 基準のままです。

## 8. Avalonia 版サンプルとの違い

同じコア・提示層を使うため、設定と見た目の意味は同じです。 UI フレームワークの違いにより次の点が異なります。

| 項目 | 本サンプル（MewUI） | Avalonia 版サンプル |
| --- | --- | --- |
| UI の記述 | C# のコードのみ（MewUI は XAML を持たない） | XAML + コードビハインド |
| 画像の受け渡し | 背景画像は `Image` コントロールへ `ImageSource` として渡す | 非所有アダプタ（`IImage` ↔ `IEsBitmap`）を経由する |
| 表示領域のスクロール | なし（ズームで全体を表示する） | `ScrollViewer` で表示領域を移動できる |
| ズームの操作 | スライダーとマウス ホイール（0.1〜5.0 倍） | マウス ホイールのみ（0.1〜5.0 倍） |
| nudge の前提 | クリックでコントロールへフォーカスを与える（フォーカスが必要） | TopLevel の購読によりフォーカス不要 |
| 選択状態の表示 | 選択矩形の座標を画面に表示する | ログ（`[Event]` 行）に表示する |
| 実行ファイルの対象 | `net10.0-windows`（Win32 のウィンドウ連携を使う） | `net10.0-windows10.0.17763.0`（`Avalonia.Desktop` を使う） |

`LoupePositionMoved`（ルーペ位置の通知）は両サンプルともログへ出すだけで、ルーペは表示しません（組み込み先のアプリがルーペを追従表示します）。
