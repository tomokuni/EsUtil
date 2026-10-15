using System.Collections.ObjectModel;
using EsUtil.Feature.Marquee.WinUI3.Sample.Logging;
using EsUtil.Feature.Marquee.WinUI3.Sample.Presentation;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace EsUtil.Feature.Marquee.WinUI3.Sample;

/// <summary>サンプルのメイン ウィンドウ（背景 + ドラッグ選択 + ログ表示）。</summary>
/// <remarks>
/// <b>示すこと</b>: コントロールの公開 API（<c>SetCoordTransform</c> / <c>Options</c> / <c>Appearance</c> /<br/>
/// <c>SetSelection</c> / <c>ResetSelection</c> / <c>RequestConfirm</c> / <c>RequestCancel</c> / <c>Log</c>）だけで<br/>
/// ドラッグ選択・表示倍率・外観の変更・確定/キャンセルの流れを組める。<br/>
/// <br/>
/// <b>座標系</b>: 選択矩形は対象座標（元画像ピクセル）で扱う。 表示の倍率は座標変換ポート<br/>
/// （<c>CoordTransform.ClientToSource</c>）が担うため、コントロールは倍率を知らない。<br/>
/// <br/>
/// <b>拡張例</b>: 見本のチェックを入れると、外観と描画計画を差し替えて外枠を破線・内側を三分割のガイド線にする<br/>
/// （提示層の派生は不要。 <see cref="GuideMarqueePlan"/>）。<br/>
/// <br/>
/// <b>ログ</b>: コントロールのログ（接頭辞は既定の <c>[Marquee]</c>）と、サンプルが受けた通知を同じ表示へ流す。
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>背景（選択対象）の幅（ピクセル。 座標変換ポートの境界として使う）。</summary>
    private const int SourceWidth = 800;

    /// <summary>背景（選択対象）の高さ（ピクセル。 座標変換ポートの境界として使う）。</summary>
    private const int SourceHeight = 600;

    /// <summary>ログ表示に残す最大行数。</summary>
    private const int MaxLogLines = 200;

    /// <summary>ルーペ位置の目印の一辺（DIP）。</summary>
    private const double LoupeMarkerSize = 24.0;

    /// <summary>表示倍率の選択肢（対象座標 / ローカル DIP）。</summary>
    private static readonly double[] s_zoomChoices = [0.5, 1.0, 1.5, 2.0];

    /// <summary>ログ表示の行。</summary>
    private readonly ObservableCollection<string> _logLines = [];

    /// <summary>コントロールへ設定するロガー（画面のログ表示へ出力する）。</summary>
    private readonly InMemoryLogger _logger;

    /// <summary>現在の表示倍率（座標変換ポートが描画のたびに読む）。</summary>
    private double _zoom = 1.0;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    /// <remarks>
    /// ウィンドウの生成時に、操作の結線と選択の初期状態（枠線・ハンドル・ラベルが見える状態）を済ませる。
    /// </remarks>
    public MainWindow()
    {
        InitializeComponent();

        _logger = new InMemoryLogger(AppendLog);
        LogList.ItemsSource = _logLines;

        ConfigureZoomSelector();
        ConfigureCommands();
        ConfigureMarquee();

        // 起動直後に枠線・ハンドル・ラベルが見えるように、選択を 1 つ与えておく
        Marquee.SetSelection(new EsRect(120, 80, 240, 180));
        AppendLog("起動しました（左ドラッグ: 選択 / 矢印キー: 微調整 / Enter: 確定 / Escape: キャンセル）");
    }

    /// <summary>表示倍率の選択肢を設定する。</summary>
    private void ConfigureZoomSelector()
    {
        ScaleSelector.ItemsSource = s_zoomChoices.Select(static zoom => $"{zoom:0.0} 倍").ToList();
        ScaleSelector.SelectedIndex = Array.IndexOf(s_zoomChoices, _zoom);
        ScaleSelector.SelectionChanged += OnScaleChanged;
    }

    /// <summary>操作のボタンとチェックを結線する。</summary>
    private void ConfigureCommands()
    {
        ClearButton.Click += OnClearClicked;
        ConfirmButton.Click += OnConfirmClicked;
        CancelButton.Click += OnCancelClicked;
        GuideSelector.Checked += OnGuideChanged;
        GuideSelector.Unchecked += OnGuideChanged;
    }

    /// <summary>コントロールへログ・座標変換ポート・通知の購読を設定する。</summary>
    private void ConfigureMarquee()
    {
        // コントロールのログを画面へ流す（接頭辞は既定の [Marquee] をそのまま使う）
        Marquee.Log = _logger;

        // 対象座標 = ローカル DIP × 表示倍率。 境界（元画像サイズ）を与えて選択を外へ出さない
        Marquee.SetCoordTransform(CoordTransform.ClientToSource(() => _zoom, () => new EsSize(SourceWidth, SourceHeight)));

        Marquee.SelectionStarted += OnSelectionStarted;
        Marquee.SelectionChanged += OnSelectionChanged;
        Marquee.SelectionCompleted += OnSelectionCompleted;
        Marquee.SelectionCancelled += OnSelectionCancelled;
        Marquee.ConfirmRequested += OnConfirmRequested;
        Marquee.CancelRequested += OnCancelRequested;
        Marquee.LoupePositionMoved += OnLoupePositionMoved;
    }

    /// <summary>表示倍率の変更を反映する（座標変換ポートの関数は描画のたびに評価される）。</summary>
    /// <param name="sender">イベント送信元（表示倍率の選択）。</param>
    /// <param name="e">選択の変更内容。</param>
    private void OnScaleChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = ScaleSelector.SelectedIndex;
        if (index < 0 || index >= s_zoomChoices.Length)
        {
            return;
        }

        _zoom = s_zoomChoices[index];
        // 倍率の変化はポートの関数が吸収するため、コントロールへ求めるのは再描画だけである
        Marquee.InvalidateRender();
        AppendLog($"表示倍率: {_zoom:0.0} 倍");
    }

    /// <summary>選択を消す。</summary>
    /// <param name="sender">イベント送信元（クリア ボタン）。</param>
    /// <param name="e">クリックの情報。</param>
    private void OnClearClicked(object sender, RoutedEventArgs e) => Marquee.ResetSelection();

    /// <summary>確定を要求する（Enter キーと同じ経路）。</summary>
    /// <param name="sender">イベント送信元（確定ボタン）。</param>
    /// <param name="e">クリックの情報。</param>
    private void OnConfirmClicked(object sender, RoutedEventArgs e) => Marquee.RequestConfirm();

    /// <summary>キャンセルを要求する（Escape キーと同じ経路）。</summary>
    /// <param name="sender">イベント送信元（キャンセル ボタン）。</param>
    /// <param name="e">クリックの情報。</param>
    private void OnCancelClicked(object sender, RoutedEventArgs e) => Marquee.RequestCancel();

    /// <summary>見本（破線とガイド線）の表示を切り替える。</summary>
    /// <param name="sender">イベント送信元（見本のチェック）。</param>
    /// <param name="e">チェックの情報。</param>
    private void OnGuideChanged(object sender, RoutedEventArgs e)
        => GuideMarqueePlan.Apply(Marquee, GuideSelector.IsChecked == true);

    /// <summary>ドラッグの始点が決まったことを記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">通知された座標（対象座標）。</param>
    private void OnSelectionStarted(object? sender, MarqueePointEventArgs e)
        => AppendLog($"SelectionStarted: ({e.Point.X:0}, {e.Point.Y:0})");

    /// <summary>選択矩形の変更を記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">通知された矩形（対象座標）。</param>
    private void OnSelectionChanged(object? sender, SelectionRectEventArgs e)
        => AppendLog($"SelectionChanged: {FormatRect(e.Rect)}");

    /// <summary>選択矩形の確定を記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">通知された矩形（対象座標）。</param>
    private void OnSelectionCompleted(object? sender, SelectionRectEventArgs e)
        => AppendLog($"SelectionCompleted: {FormatRect(e.Rect)}");

    /// <summary>選択のキャンセルを記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectionCancelled(object? sender, EventArgs e) => AppendLog("SelectionCancelled");

    /// <summary>確定要求を記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">通知された矩形（対象座標）。</param>
    private void OnConfirmRequested(object? sender, SelectionRectEventArgs e)
        => AppendLog($"ConfirmRequested: {FormatRect(e.Rect)}");

    /// <summary>キャンセル要求を記録する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    private void OnCancelRequested(object? sender, EventArgs e) => AppendLog("CancelRequested");

    /// <summary>ルーペの中心の通知を目印の位置へ反映する。</summary>
    /// <param name="sender">イベント送信元（コントロール）。</param>
    /// <param name="e">通知された中心（対象座標）。</param>
    /// <remarks>
    /// 通知は対象座標（元画像ピクセル）で届くため、表示 DIP へ戻して置く（座標変換ポートの逆変換と同じ規則）。<br/>
    /// 実際のルーペ（拡大表示）はアプリ固有の付加情報であり、本サンプルは位置だけを目印で示す。
    /// </remarks>
    private void OnLoupePositionMoved(object? sender, MarqueePointEventArgs e)
    {
        var dip = new EsPoint(e.Point.X / _zoom, e.Point.Y / _zoom);

        Canvas.SetLeft(LoupeMarker, dip.X - (LoupeMarkerSize / 2));
        Canvas.SetTop(LoupeMarker, dip.Y - (LoupeMarkerSize / 2));
        LoupeMarker.Visibility = Visibility.Visible;
    }

    /// <summary>矩形を読みやすい書式にする。</summary>
    /// <param name="rect">対象の矩形（対象座標）。</param>
    /// <returns>左上と寸法の文字列。</returns>
    private static string FormatRect(EsRect rect)
        => $"({rect.X:0}, {rect.Y:0}, {rect.Width:0} x {rect.Height:0})";

    /// <summary>ログ表示へ 1 行追加する。</summary>
    /// <param name="line">追加する行。</param>
    /// <remarks>表示は直近の <see cref="MaxLogLines"/> 行だけを保つ（際限なく増やさない）。</remarks>
    private void AppendLog(string line)
    {
        _logLines.Add(line);
        while (_logLines.Count > MaxLogLines)
        {
            _logLines.RemoveAt(0);
        }

        LogList.ScrollIntoView(_logLines[^1]);
    }
}
