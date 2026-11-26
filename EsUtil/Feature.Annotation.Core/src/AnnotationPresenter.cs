using System.Collections.Immutable;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using EsUtil.Feature.Annotation.Core.Geometry;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Core.Text;
using EsUtil.Feature.StyledText.Core.Defaults;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の描画計画を解決する提示層（Core と同一アセンブリ）。</summary>
/// <remarks>
/// <b>幾何と折り返しを解決し、UI へ計画を渡す</b>: 文字の実寸だけは描画 API に依存するため
/// <c>measureText</c> として UI から受け取る（ルーペの <c>measureText</c> と同じ形）。<br/>
/// <b>選択枠は解決しない</b>: 選択中の枠とハンドルは Marquee の提示層が解決する（枠の見た目を
/// 4 つの UI 版で揃えるため、同じ仕組みを使う）。<br/>
/// <b>毎フレームの解決は軽い</b>（比例計算と折り返しのみ。 画像を持たず、描画 API を呼ばない）。
/// </remarks>
public sealed class AnnotationPresenter
{
    /// <summary>行間の倍率の既定値（文字の実寸の高さに対する行の高さ）。</summary>
    /// <remarks>日本語の本文が詰まって見えないよう、実寸より少し広く取る。</remarks>
    public const double LineHeightFactorDefault = 1.25;

    /// <summary>行間の倍率の保持先。</summary>
    private double _lineHeightFactor = LineHeightFactorDefault;

    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>提示層の生成の記録を出力済みかどうか（ロガーの初回設定時に 1 度だけ出力する）。</summary>
    private bool _createdLogged;

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>文字の実寸の高さに対する行の高さの比。 既定は <see cref="LineHeightFactorDefault"/>。 0 以下・非有限は既定値へ補正する。</value>
    /// <remarks>変化したときだけ <c>Information</c> で記録する（本層が単一所有する設定のため）。</remarks>
    public double LineHeightFactor
    {
        get => _lineHeightFactor;
        set
        {
            var resolved = TextLimits.CoercePositive(value, LineHeightFactorDefault);
            if (_lineHeightFactor.Equals(resolved))
            {
                return;
            }

            var previous = _lineHeightFactor;
            _lineHeightFactor = resolved;
            _log.LogSettingChanged(nameof(LineHeightFactor), previous, resolved);
        }
    }

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>
    /// <b>生成の記録</b>: 本プロパティの初回の設定時に提示層の生成を 1 度だけ記録する<br/>
    /// （生成時は出力先が未設定のため、その場では記録できない。 2 回目以降の設定では繰り返さない）。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            // 出力先を所有するのはファサードである（本層は「接続した」を記録しない。 同じ 1 回の設定で
            // 2 行出ると、どの層の設定か読み分けられない。 Loupe / Marquee と同じ分担にする）。
            _log = value ?? NullLogger.Instance;
            LogPresenterCreatedOnce();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>ログ定義（<see cref="AnnotationLogMessage"/>）の接頭辞を設定する（コアと提示層は同じ定義を共有する）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            AnnotationLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? AnnotationLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary>ロガーが設定された時点で提示層の生成を 1 度だけ出力する。</summary>
    private void LogPresenterCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _log.LogPresenterCreated(_lineHeightFactor);
    }

    /// <summary>描画計画を解決する。</summary>
    /// <param name="document">対象の文書。</param>
    /// <param name="measureText">文字列と書式から実寸（元画像ピクセル）を測る関数。</param>
    /// <param name="selectedId">選択中の注釈の識別子（無い場合は <see langword="null"/>）。</param>
    /// <returns>1 フレーム分の描画計画。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="measureText"/> が null の場合。</exception>
    public AnnotationDrawPlan ResolveDrawPlan(
        AnnotationDocument document,
        Func<string, TextStyle, EsSize> measureText,
        string? selectedId = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(measureText);

        var imageSize = AnnotationGeometry.ResolveImageSize(document);
        var shapes = ImmutableArray.CreateBuilder<AnnotationShapePlan>(document.Items.Length);
        foreach (var item in document.Items)
        {
            // **描画の直前に画像の内側へ収める**: 画像の外へ出た注釈は見えず掴めない。
            // 追加・移動の経路が増えても、描画の入口で必ず収まるようにする。
            //
            // **種類ごとの描き方は 1 か所で決める**: 選択・移動・当たり判定・ポインターの形は
            // すべて `Bounds` を使うため、種類を増やすときに触るのはここだけである
            // （新しい種類は `AnnotationItem` を派生させ、この分岐へ 1 つ足す）。
            shapes.Add(item switch
            {
                BubbleAnnotation bubble => ResolveBubble(
                    ClampBubble(bubble, imageSize), measureText, LineHeightFactor),
                FrameAnnotation frame => ResolveFrame(ClampFrame(frame, imageSize)),

                // 未知の種類は矩形として描く（読み込んだファイルに新しい種類が入っていても、
                // 見えなくなる・掴めなくなることを避ける）
                _ => ResolveFrame(ClampFrame(item, imageSize)),
            });
        }

        return new AnnotationDrawPlan(
            imageSize,
            selectedId,
            shapes.ToImmutable());
    }

    /// <summary>枠線の注釈を画像の内側へ収めた値を返す。</summary>
    /// <param name="item">対象の注釈。</param>
    /// <param name="imageSize">画像の大きさ。</param>
    /// <returns>収めた注釈（収まっている場合は元の注釈）。</returns>
    private static AnnotationItem ClampFrame(AnnotationItem item, EsSize imageSize)
    {
        var bounds = AnnotationGeometry.ClampRect(item.Bounds, imageSize);
        return bounds.NearlyEquals(item.Bounds) ? item : item with { Bounds = bounds };
    }

    /// <summary>吹き出しの注釈を画像の内側へ収めた値を返す。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="imageSize">画像の大きさ。</param>
    /// <returns>収めた吹き出し（収まっている場合は元の吹き出し）。</returns>
    private static BubbleAnnotation ClampBubble(BubbleAnnotation bubble, EsSize imageSize)
    {
        var bounds = AnnotationGeometry.ClampRect(bubble.Bounds, imageSize);
        var tip = AnnotationGeometry.ClampTailTip(bubble.Tail.Tip, imageSize);
        return bounds.NearlyEquals(bubble.Bounds) && tip.NearlyEquals(bubble.Tail.Tip)
            ? bubble
            : bubble with { Bounds = bounds, Tail = bubble.Tail with { Tip = tip } };
    }

    /// <summary>枠線の注釈の計画を解決する。</summary>
    /// <param name="item">対象の注釈。</param>
    /// <returns>描画計画。</returns>
    private static AnnotationShapePlan ResolveFrame(AnnotationItem item)
    {
        var appearance = item.Appearance.Normalize();
        return new AnnotationShapePlan(
            item.Id,
            AnnotationKind.Frame,
            item.Bounds,
            appearance.CornerRadius,
            appearance.Fill,
            appearance.Border,
            appearance.BorderThickness,
            [],
            []);
    }

    /// <summary>吹き出しの注釈の計画を解決する（折り返しと行の配置を含む）。</summary>
    /// <param name="bubble">対象の吹き出し。</param>
    /// <param name="measureText">実寸を測る関数。</param>
    /// <param name="lineHeightFactor">行間の倍率。</param>
    /// <returns>描画計画。</returns>
    private static AnnotationShapePlan ResolveBubble(
        BubbleAnnotation bubble, Func<string, TextStyle, EsSize> measureText, double lineHeightFactor)
    {
        var appearance = bubble.Appearance.Normalize();
        var body = bubble.ResolveBodyRect();

        // 折り返しと行の配置は文字の計算層が単一所有する（4 つの UI 版で同じ位置に折り返す）
        var lines = TextLayout.ResolveLines(bubble.Text, body, measureText, lineHeightFactor);
        var tail = bubble.Tail.Normalize();
        var (from, to) = tail.ResolveBase(bubble.Bounds);
        var polygon = new List<EsPoint>(3) { tail.Tip, from, to };

        return new AnnotationShapePlan(
            bubble.Id,
            AnnotationKind.Bubble,
            bubble.Bounds,
            appearance.CornerRadius,
            appearance.Fill,
            appearance.Border,
            appearance.BorderThickness,
            polygon,
            lines);
    }
}
