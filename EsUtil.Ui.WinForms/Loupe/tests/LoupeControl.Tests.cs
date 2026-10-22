using System.Globalization;
using EsUtil.Ui.WinForms.Loupe.Internal;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Xunit;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using DrawingColor = System.Drawing.Color;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace EsUtil.Ui.WinForms.Loupe.Tests;

/// <summary><see cref="LoupeControl"/> のプロパティと描画計画の解決のテスト。</summary>
/// <remarks>
/// <b>ハンドルを作らない</b>: メッセージ ループを持たないテストではウィンドウを持てないため、<br/>
/// ハンドルの生成を必要とする操作（<see cref="Control.CreateControl"/> 等）は呼ばない。<br/>
/// プロパティの補正・表示レイアウトの解決・描画計画の解決はハンドル無しで検証できる。<br/>
/// 画素の検証は <see cref="LoupeGraphicsPainter"/> のテストが担う（描画面はビットマップから作る）。
/// </remarks>
public class LoupeControlTests
{
    // ====================================================================
    // 既定値と補正
    // ====================================================================

    /// <summary>既定値がコアの既定値と一致することを検証する。</summary>
    [Fact]
    internal void 既定値がコアの既定値と一致する()
    {
        using var control = new LoupeControl();

        Assert.Null(control.Source);
        Assert.Null(control.CenterPoint);
        Assert.Equal(RequestedSizeDefault, control.Options.RequestedSize);
        Assert.Equal(ZoomLevelDefault, control.Options.ZoomLevel);
        Assert.Equal(ClientToPixelScalingDefault, control.ClientToPixelScaling);

        // 寸法の既定値はコア（LoupeOptions）が単一所有する（すべての UI で同一）
        Assert.Equal(BorderThicknessDefault, control.Appearance.BorderThickness);
        Assert.True(control.Appearance.ShowCoordLabel);

        // 色の既定値は描画の設定が単一所有する
        Assert.Equal(LoupeAppearance.Default, control.Appearance);
    }

    /// <summary>表示サイズがコアの安全域へ補正されることを検証する。</summary>
    /// <param name="min">設定する最小値（補正前）。</param>
    /// <param name="max">設定する最大値（補正前）。</param>
    /// <remarks>補正は設定の値の型（<see cref="LoupeOptions"/>）が持つため、設定の経路によらず範囲内の値だけが保持される。</remarks>
    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(0, 100_000)]
    internal void 表示サイズが安全域へ補正される(int min, int max)
    {
        using var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = min } };
        Assert.Equal(RequestedSizeMin, control.Options.RequestedSize);

        control.Options = control.Options with { RequestedSize = max };
        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
    }

    /// <summary>拡大率が安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 拡大率が安全域へ補正される()
    {
        using var control = new LoupeControl { Options = LoupeOptions.Default with { ZoomLevel = 0 } };
        Assert.Equal(ZoomLevelMin, control.Options.ZoomLevel);

        control.Options = control.Options with { ZoomLevel = 1000 };
        Assert.Equal(ZoomLevelMax, control.Options.ZoomLevel);
    }

    /// <summary>スケーリング係数が安全域へ補正されることを検証する。</summary>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">期待する補正結果。</param>
    [Theory]
    [InlineData(double.NaN, ClientToPixelScalingDefault)]
    [InlineData(double.PositiveInfinity, ClientToPixelScalingDefault)]
    [InlineData(0.0, ClientToPixelScalingMin)]
    [InlineData(100.0, ClientToPixelScalingMax)]
    internal void スケーリング係数が安全域へ補正される(double value, double expected)
    {
        using var control = new LoupeControl { ClientToPixelScaling = value };

        Assert.Equal(expected, control.ClientToPixelScaling);
    }

    /// <summary>座標ラベルのフォントサイズが安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 座標ラベルのフォントサイズが安全域へ補正される()
    {
        using var control = new LoupeControl { Appearance = LoupeAppearance.Default with { CoordLabelFontSize = 0 } };
        Assert.Equal(LoupeAppearance.LabelFontSizeMin, control.Appearance.CoordLabelFontSize);

        control.Appearance = control.Appearance with { CoordLabelFontSize = double.NaN };
        Assert.Equal(LoupeAppearance.CoordLabelFontSizeDefault, control.Appearance.CoordLabelFontSize);
    }

    /// <summary>枠線の太さが安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 枠線の太さが安全域へ補正される()
    {
        using var control = new LoupeControl { Appearance = LoupeAppearance.Default with { BorderThickness = 0 } };
        Assert.Equal(BorderThicknessMin, control.Appearance.BorderThickness);

        control.Appearance = control.Appearance with { BorderThickness = 1000 };
        Assert.Equal(BorderThicknessMax, control.Appearance.BorderThickness);
    }

    /// <summary>ソース画像を設定できることを検証する。</summary>
    [Fact]
    internal void ソース画像を設定できる()
    {
        using var source = TestHelper.CreateBitmap(8, 6, DrawingColor.Red);
        using var control = new LoupeControl { Source = source };

        Assert.Same(source, control.Source);
    }

    /// <summary>外観の色と表示可否を設定できることを検証する。</summary>
    /// <remarks>描画結果（色）は <see cref="LoupeGraphicsPainter"/> のテストが検証する。</remarks>
    [Fact]
    internal void 外観を設定できる()
    {
        using var control = new LoupeControl
        {
            CenterPoint = new EsPoint(10, 20),
            Appearance = LoupeAppearance.Default with
            {
                ShowCoordLabel = false,
                CrossColor = DrawingColor.FromArgb(255, 255, 0, 0),
                PasteboardColor = DrawingColor.FromArgb(255, 0, 0, 0),
                BorderColor = DrawingColor.FromArgb(255, 0, 0, 255),
                CoordLabelColor = DrawingColor.FromArgb(255, 255, 255, 0),
                CoordLabelBackgroundColor = DrawingColor.FromArgb(128, 0, 0, 0),
            },
        };

        Assert.Equal(new EsPoint(10, 20), control.CenterPoint);
        Assert.Equal(DrawingColor.FromArgb(255, 255, 0, 0), control.Appearance.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0, 0, 0), control.Appearance.PasteboardColor);
        Assert.False(control.Appearance.ShowCoordLabel);
    }

    // ====================================================================
    // 表示レイアウト（表示サイズ・表示位置。 ホストが配置に使う）
    // ====================================================================

    /// <summary>表示レイアウトが要求サイズの正方形とオフセット付きの位置を返すことを検証する。</summary>
    [Fact]
    internal void ResolveLayout_要求サイズの正方形と位置を返す()
    {
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 120 },
            CenterPoint = new EsPoint(300, 200),
            PositionOffset = 24,
        };

        var layout = control.ResolveLayout(new EsSize(1000, 1000));

        Assert.Equal(120, layout.ClientSize);

        // 位置は中心（DIP）+ オフセット（親領域内へ補正済み）
        Assert.Equal(new EsPoint(324, 224), layout.Position);
    }

    /// <summary>拡大中心が未設定なら位置を返さないことを検証する。</summary>
    /// <remarks>「配置しない」の判断はコアが単一所有する（表示サイズは要求サイズのまま返る）。</remarks>
    [Fact]
    internal void ResolveLayout_中心未設定なら位置を返さない()
    {
        using var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 120 } };

        var layout = control.ResolveLayout(new EsSize(1000, 1000));

        Assert.Equal(120, layout.ClientSize);
        Assert.Null(layout.Position);
    }

    /// <summary>親領域が表示サイズを収められない場合は位置を返さないことを検証する。</summary>
    [Fact]
    internal void ResolveLayout_親領域が収められないなら位置を返さない()
    {
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 300 },
            CenterPoint = new EsPoint(10, 10),
        };

        Assert.Null(control.ResolveLayout(new EsSize(120, 120)).Position);
    }

    /// <summary>DIP の長さがクライアント座標（物理ピクセル）へ換算されることを検証する。</summary>
    /// <remarks>ホストが <see cref="LoupeControl.ResolveLayout"/> の結果を配置へ写すための入口である。</remarks>
    [Fact]
    internal void ToClientPixel_DIPを物理ピクセルへ換算する()
    {
        using var control = new LoupeControl { ClientToPixelScaling = 2.0 };

        Assert.Equal(240, control.ToClientPixel(120));
        // 切り上げ・最低 1（0 以下の寸法でも配置できる値を返す）
        Assert.Equal(1, control.ToClientPixel(0));
    }

    /// <summary>クライアント座標（物理ピクセル）のサイズが DIP へ換算されることを検証する。</summary>
    [Fact]
    internal void ToDipSize_クライアントサイズをDIPへ換算する()
    {
        using var control = new LoupeControl { ClientToPixelScaling = 2.0 };

        var size = control.ToDipSize(new Size(300, 200));

        Assert.Equal(150, size.Width);
        Assert.Equal(100, size.Height);
    }

    // ====================================================================
    // 描画計画の解決
    // ====================================================================

    /// <summary>描画計画の解決が実効倍率をクライアント領域から求めて保持することを検証する。</summary>
    /// <remarks>
    /// 実効倍率は「クライアント領域（物理ピクセル）÷ その DIP 換算」の実測であり、DIP 換算は DPI に依る<br/>
    /// （DPI の変更通知が届かない環境でも、クライアント領域から倍率を求めて描画を破綻させない）。
    /// </remarks>
    [Fact]
    internal void ApplyClientPixelScaling_クライアント領域から実効倍率を解決する()
    {
        using var control = new LoupeControl();
        control.Size = new Size(300, 300);

        var scaling = control.ApplyClientPixelScaling();

        // DPI から換算した値（補正済み）と一致する（DPI が異なる環境でも成立する）
        var expected = CoerceClientToPixelScaling(DpiScaler.ToScaling(control.DeviceDpi));
        Assert.Equal(expected, scaling, 3);
        Assert.Equal(expected, control.ClientToPixelScaling, 3);
    }

    /// <summary>クライアント領域が未確定の場合は DPI から換算した値を使うことを検証する。</summary>
    [Fact]
    internal void ApplyClientPixelScaling_クライアント領域が未確定ならDPIから換算する()
    {
        using var control = new LoupeControl();
        control.Size = Size.Empty;

        var scaling = control.ApplyClientPixelScaling();

        Assert.Equal(CoerceClientToPixelScaling(DpiScaler.ToScaling(control.DeviceDpi)), scaling, 3);
    }

    /// <summary>描画計画の描画面がクライアント領域と同じ物理ピクセル数になることを検証する。</summary>
    /// <remarks>
    /// 描画面がクライアント領域より小さいと端に下地が残り、大きいと描画が切り取られる。<br/>
    /// したがって「実効倍率 × 表示サイズ（DIP）= クライアント領域」でなければならない。
    /// </remarks>
    [Fact]
    internal void ResolveCurrentPlan_描画面がクライアント領域と一致する()
    {
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 120 },
            CenterPoint = new EsPoint(50, 50),
        };
        control.Size = new Size(300, 200);
        control.ApplyClientPixelScaling();

        var plan = control.ResolveCurrentPlan();

        Assert.Equal(control.ClientSize.Width, plan.ClientPixelSize);
        Assert.Equal(control.ClientSize.Width, (int)plan.ClientPixelRect.Width);
    }

    /// <summary>描画計画のラベルが文言と実寸から解決されることを検証する。</summary>
    [Fact]
    internal void ResolveCurrentPlan_ラベルを実寸から解決する()
    {
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 200 },
            CenterPoint = new EsPoint(12, 34),
        };
        control.Size = new Size(200, 200);

        var plan = control.ResolveCurrentPlan();

        var label = Assert.Single(plan.Labels);

        // 文言はコアの値型が単一所有する（本コントロールは描画層へ実寸測定を渡すだけである）
        Assert.Equal("(12, 34)", label.Text);
        Assert.True(label.IsVisible);

        // 背景の大きさは「文字の実寸 + 内側余白 × 2」である（測る API と描く API が同じ実寸を使う）
        var textSize = DrawingLabelText.Measure(label.Text, label.FontSize, control.ClientToPixelScaling);
        Assert.Equal(textSize.Width + (label.PaddingX * 2), label.DipRect.Width, 3);
        Assert.Equal(textSize.Height + (label.PaddingY * 2), label.DipRect.Height, 3);
    }

    /// <summary>拡大中心が未設定ならラベルを解決しないことを検証する。</summary>
    [Fact]
    internal void ResolveCurrentPlan_中心未設定ならラベルを解決しない()
    {
        using var control = new LoupeControl();
        control.Size = new Size(200, 200);

        Assert.Empty(control.ResolveCurrentPlan().Labels);
    }

    /// <summary>同じ入力では描画計画を再解決せず、入力が変わると再解決することを検証する。</summary>
    /// <remarks>
    /// キャッシュのキーは「表示サイズ（DIP）」と「スケーリング係数」である<br/>
    /// （後者は描画の直前に書き戻されるため、書き戻し後の解決を取りこぼさないようキーに含める）。
    /// </remarks>
    [Fact]
    internal void ResolveCurrentPlan_同じ入力では再解決しない()
    {
        var calls = 0;
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = plan =>
            {
                calls++;
                return plan;
            },
        };
        control.Size = new Size(100, 100);

        control.ResolveCurrentPlan();
        Assert.Equal(1, calls);

        // 同じ入力での再解決は行わない
        control.ResolveCurrentPlan();
        Assert.Equal(1, calls);

        // スケーリング係数が変われば再解決する
        control.ClientToPixelScaling = 2.0;
        control.ResolveCurrentPlan();
        Assert.Equal(2, calls);

        // 表示サイズが変われば再解決する
        control.Size = new Size(120, 120);
        control.ResolveCurrentPlan();
        Assert.Equal(3, calls);
    }

    /// <summary>状態の変更で描画計画が再解決されることを検証する。</summary>
    /// <remarks>計画に効く入力（拡大中心・設定・外観）の変更は保持中の計画を破棄する。</remarks>
    [Fact]
    internal void ResolveCurrentPlan_状態の変更で再解決する()
    {
        var calls = 0;
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = plan =>
            {
                calls++;
                return plan;
            },
        };
        control.Size = new Size(100, 100);

        control.ResolveCurrentPlan();
        Assert.Equal(1, calls);

        control.CenterPoint = new EsPoint(20, 20);
        control.ResolveCurrentPlan();
        Assert.Equal(2, calls);

        control.Appearance = control.Appearance with { ShowCrosshair = false };
        control.ResolveCurrentPlan();
        Assert.Equal(3, calls);

        control.Options = control.Options with { ZoomLevel = 2 };
        control.ResolveCurrentPlan();
        Assert.Equal(4, calls);
    }

    /// <summary>描画計画の差し替えが計画へ反映されることを検証する。</summary>
    /// <remarks>
    /// 差し替えは「矩形の並びで表せる装飾」と「ラベルの文言・位置」の入口である<br/>
    /// （描画側は差し替え後の計画だけを見るため、層の順序も差し替え後の値に従う）。
    /// </remarks>
    [Fact]
    internal void ResolveCurrentPlan_差し替えが計画へ反映される()
    {
        using var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = static plan => plan with { CrosshairPixelRects = [new EsRect(1, 2, 3, 4)] },
        };
        control.Size = new Size(100, 100);

        var plan = control.ResolveCurrentPlan();

        Assert.Equal(new EsRect(1, 2, 3, 4), Assert.Single(plan.CrosshairPixelRects));
    }

    /// <summary>静的描画メソッドが描画実装へ委譲することを検証する（公開 API の入口）。</summary>
    [Fact]
    internal void DrawPlan_描画実装へ委譲する()
    {
        var background = DrawingColor.FromArgb(255, 255, 0, 0);
        using var bitmap = new Bitmap(16, 16, DrawingPixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var plan = CreatePlan(background);

        LoupeControl.DrawPlan(graphics, null, in plan);

        // 中心座標ラベルは表示しない（背景のアルファが画素を覆うため、ペーストボードだけを検証する）
        Assert.Equal(background.ToArgb(), bitmap.GetPixel(3, 3).ToArgb());
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>プロパティ変更がログ定義で記録されることを検証する。</summary>
    /// <param name="propertyName">変更するプロパティ名（ログの設定名と一致する）。</param>
    /// <remarks>
    /// 記録は値を所有する層が行う（画像は本コントロール、設定・環境値・拡大中心はコア）。<br/>
    /// 配線が外れるとビルドもテストも緑のままログだけ消えるため、全プロパティの記録を検証する。
    /// </remarks>
    [Theory]
    [InlineData(nameof(LoupeControl.Source))]
    [InlineData(nameof(LoupeControl.CenterPoint))]
    [InlineData(nameof(LoupeOptions.RequestedSize))]
    [InlineData(nameof(LoupeControl.ClientToPixelScaling))]
    [InlineData(nameof(LoupeOptions.ZoomLevel))]
    internal void プロパティ変更がログへ記録される(string propertyName)
    {
        var logger = new RecordingLogger();
        using var control = new LoupeControl { Log = logger };
        using var source = TestHelper.CreateBitmap(8, 6, DrawingColor.Red);

        switch (propertyName)
        {
            case nameof(LoupeControl.Source):
                control.Source = source;
                break;
            case nameof(LoupeControl.CenterPoint):
                control.CenterPoint = new EsPoint(1, 2);
                break;
            case nameof(LoupeOptions.RequestedSize):
                control.Options = control.Options with { RequestedSize = 200 };
                break;
            case nameof(LoupeControl.ClientToPixelScaling):
                control.ClientToPixelScaling = 2.0;
                break;
            default:
                control.Options = control.Options with { ZoomLevel = 8 };
                break;
        }

        Assert.Contains(logger.Messages, message => message.Contains(propertyName, StringComparison.Ordinal));
    }

    /// <summary>拡大元画像のログが同一インスタンスの再設定では出力されないことを検証する。</summary>
    [Fact]
    internal void 拡大元画像の再設定はログへ記録しない()
    {
        var logger = new RecordingLogger();
        using var control = new LoupeControl { Log = logger };
        using var source = TestHelper.CreateBitmap(8, 6, DrawingColor.Red);

        control.Source = source;
        control.Source = source;

        // ロガーの設定（生成の記録）など他のメッセージが混じるため、画像の変更の記録だけを数える
        Assert.Single(logger.Messages, message => message.Contains("Setting: Source", StringComparison.Ordinal));
    }

    /// <summary>変更ログに記録される値が補正後の値であることを検証する。</summary>
    [Fact]
    internal void 変更ログは補正後の値を記録する()
    {
        var logger = new RecordingLogger();
        using var control = new LoupeControl { Log = logger };

        control.Options = control.Options with { RequestedSize = 100_000 };

        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(RequestedSizeMax.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="pasteboard">ペーストボードの色。</param>
    /// <returns>描画計画。</returns>
    private static LoupeDrawPlan CreatePlan(DrawingColor pasteboard)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = 16, ZoomLevel = 2 },
            Appearance = LoupeAppearance.Default with { PasteboardColor = pasteboard, ShowCoordLabel = false },
            CenterPoint = new EsPoint(8, 8),
        };

        return presenter.ResolveDrawPlan(16, static (_, _) => default);
    }
}
