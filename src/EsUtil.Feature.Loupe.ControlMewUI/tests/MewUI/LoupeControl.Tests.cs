using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using EsUtil.Platform.Core.Geometry;
using System.Globalization;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;
using EsUtil.SaveEvidence.Platform.Skia;


namespace EsUtil.Feature.Loupe.MewUI.Tests;

/// <summary><see cref="LoupeControl"/> のプロパティとレイアウトのテスト。</summary>
/// <remarks>
/// MewUI の要素生成はプラットフォームホストの登録を必要としないため、UI を表示せずに検証できる。<br/>
/// 描画結果の検証は <c>EsUtil.Feature.Loupe.Presenter</c> のテストが担う。
/// </remarks>
public class LoupeControlTests
{
    /// <summary>既定値がコアの既定値と一致することを検証する。</summary>
    [Fact]
    internal void 既定値がコアの既定値と一致する()
    {
        var control = new LoupeControl();

        Assert.Null(control.Source);
        Assert.Null(control.CenterPoint);
        Assert.Equal(RequestedSizeDefault, control.Options.RequestedSize);
        Assert.Equal(ZoomLevelDefault, control.Options.ZoomLevel);
        Assert.Equal(ClientToPixelScalingDefault, control.ClientToPixelScaling);

        // 寸法の既定値はコア（LoupeOptions）が単一所有する（両 UI で同一）
        Assert.Equal(BorderThicknessDefault, control.Appearance.BorderThickness);
        Assert.True(control.Appearance.ShowCoordLabel);
        // 色の既定値は描画の設定が単一所有する
        Assert.Equal(LoupeAppearance.Default, control.Appearance);
    }

    /// <summary>数値プロパティがコアの安全域へ補正されることを検証する。</summary>
    /// <param name="min">安全域の最小値。</param>
    /// <param name="max">安全域の最大値。</param>
    [Theory]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(0, 100_000)]
    internal void 表示サイズが安全域へ補正される(int min, int max)
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = min } };
        Assert.Equal(RequestedSizeMin, control.Options.RequestedSize);

        control.Options = control.Options with { RequestedSize = max };
        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
    }

    /// <summary>拡大率が安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 拡大率が安全域へ補正される()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { ZoomLevel = 0 } };
        Assert.Equal(ZoomLevelMin, control.Options.ZoomLevel);

        control.Options = control.Options with { ZoomLevel = 1000 };
        Assert.Equal(ZoomLevelMax, control.Options.ZoomLevel);
    }

    /// <summary>スケーリング係数が非有限値のとき既定値へ補正されることを検証する。</summary>
    /// <param name="value">設定する値。</param>
    /// <param name="expected">期待する補正結果。</param>
    [Theory]
    [InlineData(double.NaN, ClientToPixelScalingDefault)]
    [InlineData(double.PositiveInfinity, ClientToPixelScalingDefault)]
    [InlineData(0.0, ClientToPixelScalingMin)]
    [InlineData(100.0, ClientToPixelScalingMax)]
    internal void スケーリング係数が安全域へ補正される(double value, double expected)
    {
        var control = new LoupeControl { ClientToPixelScaling = value };

        Assert.Equal(expected, control.ClientToPixelScaling);
    }

    /// <summary>座標ラベルのフォントサイズが安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 座標ラベルのフォントサイズが安全域へ補正される()
    {
        var control = new LoupeControl { Appearance = LoupeAppearance.Default with { CoordLabelFontSize = 0 } };
        Assert.Equal(6.0, control.Appearance.CoordLabelFontSize);

        control.Appearance = control.Appearance with { CoordLabelFontSize = double.NaN };
        Assert.Equal(LoupeAppearance.CoordLabelFontSizeDefault, control.Appearance.CoordLabelFontSize);
    }

    /// <summary>枠線の太さが安全域へ補正されることを検証する。</summary>
    [Fact]
    internal void 枠線の太さが安全域へ補正される()
    {
        var control = new LoupeControl
        {
            Appearance = LoupeAppearance.Default with { BorderThickness = 0 },
        };

        Assert.Equal(BorderThicknessMin, control.Appearance.BorderThickness);

        control.Appearance = control.Appearance with { BorderThickness = 1000 };

        Assert.Equal(BorderThicknessMax, control.Appearance.BorderThickness);
    }

    /// <summary>要求サイズが <see cref="LoupeOptions.RequestedSize"/> の正方形になることを検証する。</summary>
    [Fact]
    internal void 要求サイズが一辺RequestedSizeの正方形になる()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 120 } };

        control.Measure(new Size(1000, 1000));

        Assert.Equal(120, control.DesiredSize.Width);
        Assert.Equal(120, control.DesiredSize.Height);
    }

    /// <summary>要求サイズを変更すると測定結果へ反映されることを検証する。</summary>
    [Fact]
    internal void 要求サイズの変更が測定結果へ反映される()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 80 } };
        control.Measure(new Size(1000, 1000));
        Assert.Equal(80, control.DesiredSize.Width);

        control.Options = control.Options with { RequestedSize = 200 };
        control.Measure(new Size(1000, 1000));

        Assert.Equal(200, control.DesiredSize.Width);
        Assert.Equal(200, control.DesiredSize.Height);
    }

    // ====================================================================
    // 中心座標ラベル（文言・配置はコアが決め、本コントロールがテキスト要素として表示する）
    // ====================================================================

    /// <summary>コントロールを配置する（ラベル要素は配置フェーズで更新される）。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="size">配置する一辺のサイズ（DIP）。</param>
    /// <remarks>
    /// MewUI のテキスト要素の測定はグラフィックス バックエンドを必要とするため、本テストは配置のみを実行する<br/>
    /// （ラベル要素の大きさは配置矩形で決まり、測定はプラットフォーム ホストを起動するアプリが行う）。
    /// </remarks>
    private static void Layout(LoupeControl control, double size = 100)
        => control.Arrange(new Rect(0, 0, size, size));

    /// <summary>中心座標ラベルの要素（子のうち <see cref="Border"/>）を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>ラベルの背景要素。</returns>
    private static Border LabelElement(LoupeControl control) => control.Children.OfType<Border>().Single();

    /// <summary>中心座標ラベルの文言を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示されている文言。</returns>
    private static string LabelText(LoupeControl control) => ((TextBlock)LabelElement(control).Child!).Text!;

    // ====================================================================
    // 描画面の倍率（拡大画像の物理ピクセルサイズの算出元）
    // ====================================================================

    /// <summary>描画面の比から実効倍率を解決し、保持値へも反映することを検証する。</summary>
    /// <remarks>
    /// DPI の変更通知は追加直後のコントロールへ届かないため、保持値は登録時の 1.0 のままになりうる（実測）。<br/>
    /// その値で拡大画像の物理ピクセルサイズを決めると、画像がルーペ領域より小さい倍率で描かれ、右と下に<br/>
    /// ペーストボードの帯が残る（125% の環境で 1/1.25 になる不具合として発生した）。<br/>
    /// 描画面（物理ピクセル）と割り当て領域（DIP）の比を唯一の算出元にする。
    /// </remarks>
    [Fact]
    internal void ApplyClientPixelScaling_描画面の比を実効倍率として保持する()
    {
        // 125% の環境: 240 DIP の要素の描画面は 300 物理ピクセルになる
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 240 } };
        Layout(control, 240);
        Assert.Equal(ClientToPixelScalingDefault, control.ClientToPixelScaling);

        var scaling = control.ApplyClientPixelScaling(new SKImageInfo(300, 300));

        Assert.Equal(1.25, scaling, 3);
        Assert.Equal(1.25, control.ClientToPixelScaling, 3);
    }

    /// <summary>描画面の比が保持値と一致する場合はそのまま返すことを検証する。</summary>
    [Fact]
    internal void ApplyClientPixelScaling_一致する場合は保持値をそのまま返す()
    {
        // 等倍（96 DPI）の環境: 240 DIP の要素の描画面は 240 物理ピクセルになる
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 240 } };
        Layout(control, 240);

        var scaling = control.ApplyClientPixelScaling(new SKImageInfo(240, 240));

        Assert.Equal(ClientToPixelScalingDefault, scaling, 3);
        Assert.Equal(ClientToPixelScalingDefault, control.ClientToPixelScaling, 3);
    }

    /// <summary>ハイライト矩形を除いた実効倍率がルーペの描画面と一致することを検証する。</summary>
    /// <remarks>
    /// 実効倍率 × 割り当て領域（DIP）= 描画面（物理ピクセル）であり、この倍率でコアが拡大画像の<br/>
    /// 物理ピクセルサイズを決めるため、ルーペ領域（描画面）と拡大画像の大きさが一致する。
    /// </remarks>
    [Fact]
    internal void ApplyClientPixelScaling_倍率とDIPの積が描画面と一致する()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 180 } };
        Layout(control, 180);

        // 180 DIP × 1.25 = 225 物理ピクセル（切り上げなしで一致する値を使う）
        var info = new SKImageInfo(225, 225);
        var scaling = control.ApplyClientPixelScaling(info);

        Assert.Equal(info.Width, scaling * control.Bounds.Size.Width, 3);
    }

    /// <summary>中心座標を設定するとラベル要素が表示されることを検証する（中心未設定では表示しない）。</summary>
    [Fact]
    internal void 中心座標を設定するとラベル要素が表示される()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 100 } };
        Layout(control);

        // 中心未設定 → ラベルは表示しない
        Assert.Empty(control.Children.OfType<Border>());

        control.CenterPoint = new EsPoint(12, 34);
        Layout(control);

        // 文言はコアの EsPoint.Format が単一所有する（本コントロールは要 素へ写すだけ）
        Assert.Equal("(12, 34)", LabelText(control));

        // 幅は実測値（本コントロールが Skia で測ってコアへ渡す）と内側余白から決まる
        var measured = SkiaText.Measure("(12, 34)", control.Appearance.CoordLabelFontSize);
        Assert.True(LabelElement(control).Bounds.Width > measured.Width);
    }

    /// <summary>ラベル非表示の設定では要素を追加しないことを検証する。</summary>
    [Fact]
    internal void ラベル非表示の設定では要素を表示しない()
    {
        var control = new LoupeControl
        {
            Appearance = LoupeAppearance.Default with
            {
                ShowCoordLabel = false,
            },
            CenterPoint = new EsPoint(12, 34),
        };

        Layout(control);

        Assert.Empty(control.Children.OfType<Border>());
    }

    /// <summary>中心座標を変更するとラベル要素の文言が追従することを検証する。</summary>
    [Fact]
    internal void 中心座標を変更するとラベル要素の文言が追従する()
    {
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(1, 2),
        };
        Layout(control);
        Assert.Equal("(1, 2)", LabelText(control));

        control.CenterPoint = new EsPoint(3, 4);
        Layout(control);

        Assert.Equal("(3, 4)", LabelText(control));
    }

    /// <summary>寸法と色を設定できることを検証する（描画は <c>EsUtil.Feature.Loupe.Presenter</c> のテストが検証する）。</summary>
    [Fact]
    internal void 外観を設定できる()
    {
        var control = new LoupeControl
        {
            CenterPoint = new EsPoint(10, 20),
            Appearance = LoupeAppearance.Default with
            {
                ShowCoordLabel = false,
            } with
            {
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

    /// <summary>ソース画像を設定できることを検証する。</summary>
    [Fact]
    internal void ソース画像を設定できる()
    {
        using var source = TestHelper.CreateBitmap(8, SKColors.Red);
        var control = new LoupeControl { Source = source };

        Assert.Same(source, control.Source);
    }

    /// <summary>プロパティの setter を経由しない設定でもコアの安全域へ補正されることを検証する。</summary>
    /// <remarks>
    /// MewUI のバインディングはプロパティの setter を通さずに値を書き込む。<br/>
    /// 設定（<see cref="LoupeOptions"/>）の数値項目は<b>値の型の生成時</b>に補正されるため、
    /// 設定経路（オブジェクト初期化子・<c>with</c> 式・バインディング）によらず範囲内の値だけが保持される。<br/>
    /// 環境値の <see cref="LoupeControl.ClientToPixelScaling"/> はプロパティの補正コールバック（<c>coerce</c>）で補正する。<br/>
    /// setter を経由しない設定は <c>MewObject.SetValue</c>（protected）しかないため、リフレクションで呼ぶ。
    /// </remarks>
    [Fact]
    internal void 補正はプロパティの全設定経路へ適用される()
    {
        var control = new LoupeControl();

        TestHelper.SetValueWithoutSetter(control, LoupeControl.OptionsProperty, LoupeOptions.Default with
        {
            RequestedSize = 100_000,
            ZoomLevel = 1000,
        });
        TestHelper.SetValueWithoutSetter(control, LoupeControl.ClientToPixelScalingProperty, 100.0);

        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
        Assert.Equal(ZoomLevelMax, control.Options.ZoomLevel);
        Assert.Equal(ClientToPixelScalingMax, control.ClientToPixelScaling);
    }

    /// <summary>プロパティ変更がコアのログ定義で記録されることを検証する。</summary>
    /// <param name="propertyName">変更するプロパティ名（ログの設定名と一致する）。</param>
    /// <remarks>
    /// 記録はプロパティの変更通知（<c>changed</c> コールバック）が行う（setter を経由しない設定でも記録される）。<br/>
    /// 配線が外れるとビルドもテストも緑のままログだけ消えるため、全プロパティの記録を検証する。<br/>
    /// 設定のログ名はコアのログ定義（変更された項目ごと）が単一所有するため、項目名で照合する。
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
        var control = new LoupeControl { Log = logger };
        using var source = TestHelper.CreateBitmap(8, SKColors.Red);

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

    /// <summary>変更ログに記録される値が補正後の値であることを検証する。</summary>
    [Fact]
    internal void 変更ログは補正後の値を記録する()
    {
        var logger = new RecordingLogger();
        var control = new LoupeControl { Log = logger };

        control.Options = control.Options with { RequestedSize = 100_000 };

        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
        Assert.Contains(
            logger.Messages,
            message => message.Contains(RequestedSizeMax.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    // ====================================================================
    // 見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）
    // ====================================================================

    /// <summary>描画計画の差し替えが配置（ラベル要素）と描画の両方へ効くことを検証する。</summary>
    /// <remarks>
    /// 差し替えた <see cref="LoupeDrawPlan.Labels"/> は配置フェーズでラベル要素になるため、<br/>
    /// 子要素の文言で差し替えが効いたことを確認できる（キャンバスを必要としない）。
    /// </remarks>
    [Fact]
    internal void 描画計画の差し替えがラベル要素へ反映される()
    {
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = static plan => plan with
            {
                Labels = [new LoupeLabelPlacement(new EsRect(1, 2, 30, 20), 2, 2, "差し替え", 12, DrawingColor.White, DrawingColor.Black, 2)],
            },
        };

        Layout(control);

        // 差し替えた 1 件だけがラベル要素になる
        var labels = control.Children.OfType<Border>().ToList();
        Assert.Single(labels);
        Assert.Equal("差し替え", ((TextBlock)labels[0].Child!).Text);
    }

    /// <summary>外観で装飾を止めると装飾の矩形が描かれないことを検証する。</summary>
    /// <remarks>
    /// 外観の <c>Show*</c> が false の項目は計画の値と層の両方から消えるため、描画実装へ別のスイッチを渡さない。<br/>
    /// ペーストボード（描画面の消去）と拡大画像は装飾ではなく、外観にフラグも無いため常に描かれる。
    /// </remarks>
    [Fact]
    internal void 外観で装飾を止めると装飾だけが描かれない()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64));
        using var canvas = new SKCanvas(bitmap);

        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = 64, ZoomLevel = 1 },
            Appearance = LoupeAppearance.Default with
            {
                BorderThickness = 2,
                ShowCrosshair = true,
                ShowBorder = true,
            },
            CenterPoint = new EsPoint(32, 32),
        };
        var plan = presenter.ResolveDrawPlan(64, static (_, _) => default);

        // 既定（装飾を描く）: 枠線の色の画素が現れる
        LoupeSkiaPainter.Paint(canvas, null, in plan);
        var borderPixel = bitmap.GetPixel(32, 1);

        // 外観で止める: 枠線と十字線が層から消え、ペーストボードの色になる
        presenter.Appearance = presenter.Appearance with { ShowCrosshair = false, ShowBorder = false };
        var stopped = presenter.ResolveDrawPlan(64, static (_, _) => default);

        LoupeSkiaPainter.Paint(canvas, null, in stopped);
        var withoutBorderPixel = bitmap.GetPixel(32, 1);

        Assert.DoesNotContain(LoupeDrawLayer.Crosshair, stopped.Layers);
        Assert.DoesNotContain(LoupeDrawLayer.Border, stopped.Layers);
        Assert.NotEqual(borderPixel, withoutBorderPixel);
        // ペーストボード（画像外の色）は描かれる
        Assert.Equal(LoupeAppearance.Default.PasteboardColor.ToSKColor(), withoutBorderPixel);
    }

    // ====================================================================
    // 描画計画の解決の回数（1 フレーム = 1 回）
    // ====================================================================

    /// <summary>同じ入力では描画計画を再解決せず、入力が変わると再解決することを検証する。</summary>
    /// <remarks>
    /// 配置と描画はどちらも同じ表示サイズを渡すが、描画経路はキャンバスを必要とするため、本テストは再配置（配置経路）で検証する。<br/>
    /// キャッシュのキーは「表示サイズ」と「スケーリング係数」である（後者は描画の直前に書き戻されるため、<br/>
    /// 書き戻し後の解決を取りこぼさないようキーに含める）。
    /// </remarks>
    [Fact]
    internal void 同じ入力では描画計画を再解決せず入力が変わると再解決する()
    {
        var calls = 0;
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = plan => { calls++; return plan; },
        };

        Layout(control);
        Assert.Equal(1, calls);

        // 同じ入力での再配置は再解決しない
        control.InvalidateArrange();
        Layout(control);
        Assert.Equal(1, calls);

        // スケーリング係数が変われば再解決する（描画の直前の書き戻しをキーで捉える）
        control.ApplyClientPixelScaling(new SKImageInfo(125, 125));
        control.InvalidateArrange();
        Layout(control);
        Assert.Equal(2, calls);

        // 基準サイズが変われば再解決する
        control.InvalidateArrange();
        Layout(control, 120);
        Assert.Equal(3, calls);
    }

    /// <summary>状態の変更で描画計画が再解決されることを検証する。</summary>
    /// <remarks>計画に効く入力（拡大中心・設定・外観）の変更は保持中の計画を破棄する。</remarks>
    [Fact]
    internal void 状態の変更で描画計画が再解決される()
    {
        var calls = 0;
        var control = new LoupeControl
        {
            Options = LoupeOptions.Default with { RequestedSize = 100 },
            CenterPoint = new EsPoint(10, 10),
            DrawPlanResolver = plan => { calls++; return plan; },
        };

        Layout(control);
        Assert.Equal(1, calls);

        control.CenterPoint = new EsPoint(20, 20);
        Layout(control);
        Assert.Equal(2, calls);

        control.Appearance = control.Appearance with { ShowCrosshair = false };
        Layout(control);
        Assert.Equal(3, calls);

        control.Options = control.Options with { ZoomLevel = 2 };
        Layout(control);
        Assert.Equal(4, calls);
    }
}
