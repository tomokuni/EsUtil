using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Feature.Thumbnail;

/// <summary>サムネイル一覧の提示（外観・影・描画計画）を UI 層へ提供するファサード（本層の既定実装）。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（設定・配置・描画計画の解決を 1 つの窓口へ集約する）と<br/>
/// Presenter（受動的な UI 層へ「何をどこへ描くか」を解決した計画を渡す）。<br/>
/// <br/>
/// <b>責務</b>: コア（<see cref="ThumbnailLayout"/>）の配置計算を呼び、外観（<see cref="Appearance"/>）と<br/>
/// 項目の状態（ホバー・選択・現在表示中）から 1 件分の描画計画（<see cref="ThumbnailDrawPlan"/>）を解決する。<br/>
/// UI 層は本クラスだけを参照する（コアの計算クラスを個別に呼び出さない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - 外観（色・名前の体裁・影の計画）<br/>
/// - 1 件分の矩形（外枠・影・画像・名前）の導出（コアが返す配置から）<br/>
/// - 描く層とその順序（<see cref="ThumbnailDrawLayer"/>）<br/>
/// - 背景の色の決定（ホバー・選択のどちらを優先するか）<br/>
/// <br/>
/// <b>コアが単一所有するもの</b>: 折り返しの列数・行の高さ・下端揃え・項目の矩形・選択の状態・可視範囲。<br/>
/// <br/>
/// <b>本クラスは UI フレームワークに依存しない</b>（色は UI 非依存の <c>0xAARRGGBB</c>、描画は行わない）。<br/>
/// <b>差し替え点は 3 つある</b>:<br/>
/// 1. <see cref="ThumbnailAppearance"/> の値（色・体裁・影の有無）<br/>
/// 2. 影の算法（<see cref="IThumbnailShadowPlan"/> の実装を <see cref="ThumbnailAppearance.Shadow"/> へ渡す）<br/>
/// 3. <see cref="ResolveDrawPlan"/> の上書き（背景・層の順序・名前の体裁・影の有無を自前の<br/>
/// <see cref="ThumbnailDrawPlan"/> で返す）<br/>
/// このため本クラスは <c>sealed</c> にしない。<br/>
/// <br/>
/// <b>影の余白はレイアウトへ反映する</b>: 影の <see cref="IThumbnailShadowPlan.Insets"/> が<br/>
/// <see cref="ThumbnailOptions.ShadowInset"/> より広い場合は、<see cref="ResolveLayout"/> が余白を広げる。
/// </remarks>
public class ThumbnailPresenter(ThumbnailOptions? options = null, ThumbnailAppearance? appearance = null)
{
    /// <summary>背景も影も描かない層の並び。</summary>
    private static readonly IReadOnlyList<ThumbnailDrawLayer> s_imageAndLabel =
        [ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label];

    /// <summary>背景だけを描く層の並び。</summary>
    private static readonly IReadOnlyList<ThumbnailDrawLayer> s_backgroundImageAndLabel =
        [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label];

    /// <summary>影だけを描く層の並び。</summary>
    private static readonly IReadOnlyList<ThumbnailDrawLayer> s_shadowImageAndLabel =
        [ThumbnailDrawLayer.Shadow, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label];

    /// <summary>背景と影を描く層の並び。</summary>
    private static readonly IReadOnlyList<ThumbnailDrawLayer> s_allLayers =
        [ThumbnailDrawLayer.Background, ThumbnailDrawLayer.Shadow, ThumbnailDrawLayer.Image, ThumbnailDrawLayer.Label];

    /// <summary>計算に必要な設定。</summary>
    private ThumbnailOptions _options = options ?? ThumbnailOptions.Default;

    /// <summary>描画にだけ必要な外観。</summary>
    private ThumbnailAppearance _appearance = appearance ?? ThumbnailAppearance.Default;

    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>生成の記録を出力済みかどうか（ロガーの初回設定時に 1 度だけ出力する）。</summary>
    private bool _createdLogged;

    /// <summary>計算に必要な設定を取得または設定する。</summary>
    /// <value>設定。 null は設定できない（<see cref="ThumbnailOptions.Default"/> を使う）。</value>
    /// <remarks>
    /// 設定を変えたら UI は配置を解決し直す（<see cref="ResolveLayout"/>）。<br/>
    /// 変化した項目の検出と記録は本プロパティが単一所有する（ホストは同値判定を持たない）。
    /// </remarks>
    public ThumbnailOptions Options
    {
        get => _options;
        set
        {
            var next = value ?? ThumbnailOptions.Default;
            if (ReferenceEquals(next, _options))
            {
                return;
            }

            var previous = _options;
            _options = next;
            _log.LogOptionsChanged(previous, next);
        }
    }

    /// <summary>描画にだけ必要な外観を取得または設定する。</summary>
    /// <value>外観。 null は設定できない（<see cref="ThumbnailAppearance.Default"/> を使う）。</value>
    /// <remarks>
    /// 外観を変えたら UI は項目を描き直す（<see cref="ResolveDrawPlan"/> を呼び直す）。<br/>
    /// 変化した項目の検出と記録は本プロパティが単一所有する（ホストは同値判定を持たない）。
    /// </remarks>
    public ThumbnailAppearance Appearance
    {
        get => _appearance;
        set
        {
            var next = value ?? ThumbnailAppearance.Default;
            if (ReferenceEquals(next, _appearance))
            {
                return;
            }

            var previous = _appearance;
            _appearance = next;
            _log.LogAppearanceChanged(previous, next);
        }
    }

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>
    /// <b>生成の記録</b>: 本プロパティの<b>初回の設定時</b>に本層の生成を 1 度だけ記録する<br/>
    /// （生成時は出力先が未設定のため、その場では記録できない。 2 回目以降の設定では繰り返さない）。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            LogPresenterCreatedOnce();
            _log.LogLoggerAttached();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Thumbnail]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本層のログ定義（<see cref="ThumbnailLogMessage"/>）の接頭辞を設定する（本層は最下層のため伝搬先は無い）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ThumbnailLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ThumbnailLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary>ロガーが設定された時点で生成を 1 度だけ出力する。</summary>
    private void LogPresenterCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _log.LogPresenterCreated();
    }

    /// <summary>一覧の配置を解決する。</summary>
    /// <param name="availableWidth">一覧の幅（DIP）。</param>
    /// <param name="imageWidth">画像の幅（DIP）。</param>
    /// <param name="imageHeights">項目ごとの画像の高さ（DIP）。</param>
    /// <returns>配置の結果。</returns>
    /// <remarks>
    /// <b>影の余白と名前の高さをレイアウトへ反映する</b>: 外観の影（<see cref="IThumbnailShadowPlan.Insets"/>）から<br/>
    /// 影が切れない余白を求め、名前の行の高さを文字サイズから求める（利用者は設定しない）。<br/>
    /// 影の算法や文字サイズを差し替えても、項目の大きさが自動で追従する。
    /// </remarks>
    public ThumbnailLayoutResult ResolveLayout(double availableWidth, double imageWidth, ReadOnlySpan<double> imageHeights)
        => ThumbnailLayout.Resolve(availableWidth, imageWidth, imageHeights, ResolveEffectiveOptions());

    /// <summary>名前の行の最大行数（折り返して表示する上限）。</summary>
    /// <value>2。 超える場合は末尾を省略する。</value>
    public const int MaxLabelLines = 2;

    /// <summary>名前の行の行送り（文字サイズに対する倍率）。</summary>
    /// <value>1.4（文字が切れない最小の行送り）。</value>
    public const double LabelLineSpacing = 1.4;

    /// <summary>外観から求めた値を反映した設定を求める。</summary>
    /// <returns>影の余白と名前の行の高さを反映した設定。</returns>
    /// <remarks>
    /// <b>名前の行の高さは文字サイズから求める</b>（利用者に数値で指定させない）: 2 行ぶんを確保し、<br/>
    /// 1 行に収まる名前の行は余白だけを残す（行内の下端揃えで位置が揃う）。<br/>
    /// 影を描かない外観では余白 0（画像のすぐ外側に帯が付く）。
    /// </remarks>
    private ThumbnailOptions ResolveEffectiveOptions()
    {
        var shadowInset = 0.0;
        if (_appearance.Shadow is { } shadow)
        {
            var insets = shadow.Insets;
            shadowInset = Math.Max(Math.Max(insets.Left, insets.Top), Math.Max(insets.Right, insets.Bottom));
        }

        var labelHeight = Math.Ceiling(_appearance.LabelFontSize * LabelLineSpacing) * MaxLabelLines;
        return _options with { ShadowInset = shadowInset, LabelHeight = labelHeight };
    }

    /// <summary>1 件分の描画計画を解決する。</summary>
    /// <param name="layout">配置の結果。</param>
    /// <param name="index">項目の添字。</param>
    /// <param name="text">表示する名前。</param>
    /// <param name="isCurrent">現在表示中の画像かどうか。</param>
    /// <param name="selectedIndex">選択中の添字（無い場合は -1）。</param>
    /// <param name="hoveredIndex">マウスが重なっている添字（無い場合は -1）。</param>
    /// <param name="isDark">暗色テーマかどうか。</param>
    /// <returns>1 件分の描画計画。 添字が範囲外の場合は空の計画。</returns>
    public virtual ThumbnailDrawPlan ResolveDrawPlan(
        ThumbnailLayoutResult layout,
        int index,
        string text,
        bool isCurrent,
        int selectedIndex,
        int hoveredIndex,
        bool isDark)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var isSelected = index == selectedIndex;
        var isHovered = index == hoveredIndex;

        // 選択の背景をホバーより優先する（選択中の項目へ重ねて 2 色にしない）
        var background = isSelected && _appearance.ShowSelectionBackground
            ? _appearance.SelectionBackground(isDark)
            : isHovered && _appearance.ShowHoverBackground
                ? _appearance.HoverBackground(isDark)
                : 0u;

        var highlighted = isSelected || isHovered;
        var shadow = isSelected && _appearance.HideShadowWhenSelected ? null : _appearance.Shadow;
        var image = layout.Image(index);
        var shadowArea = shadow is null ? image : shadow.Expand(image);

        // 名前行は最大 2 行である（2 行に収まらない名前は拡張子を残して省略する）。
        // 文字を置ける幅は行の幅から左右の余白を除いた値であり、太字は幅が広いため少なめに見積もる
        var labelBounds = layout.Label(index);
        var isBoldLabel = isCurrent && _appearance.BoldCurrentLabel;
        var textWidth = Math.Max(0, labelBounds.Width - (_options.LabelHorizontalPadding * 2));
        var labelText = ThumbnailLabelText.Fit(
            text, textWidth, _appearance.LabelFontSize, ThumbnailLabelText.MaxLines, isBoldLabel);

        var label = new ThumbnailLabelPlan(
            labelText,
            labelBounds,
            _appearance.LabelFontSize,
            isBoldLabel,
            highlighted ? _appearance.HighlightedLabelArgb : 0,
            _appearance.LabelVerticalPadding);

        var layers = background != 0
            ? (shadow is null ? s_backgroundImageAndLabel : s_allLayers)
            : (shadow is null ? s_imageAndLabel : s_shadowImageAndLabel);

        return new ThumbnailDrawPlan(
            index, layout.Slot(index), image, shadowArea, label, layers, background, shadow, isHovered, isSelected, isCurrent);
    }

    /// <summary>画像の表示高さを求める（幅を固定し、縦横比を保つ）。</summary>
    /// <param name="width">表示幅（DIP）。</param>
    /// <param name="pixelWidth">元画像の幅（ピクセル）。</param>
    /// <param name="pixelHeight">元画像の高さ（ピクセル）。</param>
    /// <returns>表示高さ（DIP）。</returns>
    public static double ImageHeight(double width, int pixelWidth, int pixelHeight)
        => ThumbnailLayout.ImageHeight(width, pixelWidth, pixelHeight);
}
