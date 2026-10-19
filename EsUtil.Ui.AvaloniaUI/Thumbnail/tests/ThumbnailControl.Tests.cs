using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.AvaloniaUI.Thumbnail.Tests;

/// <summary>サムネイル一覧（Avalonia 版）の公開 API とログの単体テスト（ヘッドレス）。</summary>
/// <remarks>
/// コントロールの生成とプロパティ設定・公開メソッドのみを検証するため、ウィンドウ表示を必要としない。<br/>
/// 入力（ポインター）はウィンドウを必要とするため、公開 API 経由で検証できる範囲に限定する。
/// </remarks>
[Collection(AvaloniaHeadlessGroup.Name)]
public class ThumbnailControlTests
{
    /// <summary>指定サイズの画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。</returns>
    private static WriteableBitmap CreateBitmap(int width, int height)
        => new(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);

    /// <summary>項目を末尾へ追加し、追加した添字を返すことを検証する。</summary>
    [AvaloniaFact]
    internal void Add_項目を末尾へ追加する()
    {
        var control = new ThumbnailControl();

        Assert.Equal(0, control.Add(null, "a"));
        Assert.Equal(1, control.Add(null, "b"));
        Assert.Equal(2, control.ItemCount);
    }

    /// <summary>選択の設定と範囲外の解除を検証する。</summary>
    [AvaloniaFact]
    internal void SelectedIndex_範囲外は選択を解除する()
    {
        var control = new ThumbnailControl();
        control.Add(null, "a");
        control.Add(null, "b");
        var raised = 0;
        control.SelectionChanged += (_, _) => raised++;

        control.SelectedIndex = 1;
        Assert.Equal(1, control.SelectedIndex);

        control.SelectedIndex = 5;
        Assert.Equal(-1, control.SelectedIndex);
        Assert.Equal(2, raised);
    }

    /// <summary>一覧を空にすると項目数が 0 になることを検証する。</summary>
    [AvaloniaFact]
    internal void ClearItems_項目を空にする()
    {
        var control = new ThumbnailControl();
        control.Add(CreateBitmap(8, 8), "a");
        control.Add(CreateBitmap(8, 8), "b");

        control.ClearItems();

        Assert.Equal(0, control.ItemCount);
    }

    /// <summary>範囲外の添字では何もしないことを検証する。</summary>
    [AvaloniaFact]
    internal void SetImage_範囲外の添字では何もしない()
    {
        var control = new ThumbnailControl();
        control.Add(null, "a");

        control.SetImage(-1, null);
        control.SetImage(5, null);
        control.SetCaption(5, "x");

        Assert.Equal(1, control.ItemCount);
    }

    /// <summary>項目が無い場合の可視範囲が空になることを検証する。</summary>
    [AvaloniaFact]
    internal void VisibleRange_項目が無い場合は空になる()
    {
        var control = new ThumbnailControl();

        Assert.True(control.VisibleRange.IsEmpty);
        Assert.True(control.SlotBounds(0).IsEmpty);
    }

    /// <summary>計測後の項目の矩形が内容の座標で返ることを検証する。</summary>
    [AvaloniaFact]
    internal void SlotBounds_計測後に矩形を返す()
    {
        var control = new ThumbnailControl();
        control.Add(null, "a");
        control.Add(null, "b");

        var window = new Window { Width = 400, Height = 300, Content = control };
        window.Show();
        window.UpdateLayout();

        var bounds = control.SlotBounds(0);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        Assert.True(control.SlotBounds(5).IsEmpty);

        window.Close();
    }

    /// <summary>項目数の変更がログへ記録されることを検証する。</summary>
    [AvaloniaFact]
    internal void Add_と_ClearItems_は項目数を記録する()
    {
        var logger = new RecordingLogger();
        var original = ThumbnailLogMessage.Prefix;

        try
        {
            var control = new ThumbnailControl { Log = logger, LogPrefix = "[Test]" };
            logger.Messages.Clear();
            logger.Levels.Clear();
            control.Add(null, "a");
            control.Add(null, "b");
            control.ClearItems();
        }
        finally
        {
            ThumbnailLogMessage.Prefix = original;
        }

        Assert.Equal(
            [
                "[Test] Setting: ItemCount 0 → 1",
                "[Test] Setting: ItemCount 1 → 2",
                "[Test] Setting: ItemCount 2 → 0",
            ],
            logger.Messages);
        Assert.All(logger.Levels, level => Assert.Equal(LogLevel.Debug, level));
    }

    /// <summary>画像の差し替えがログへ記録されることを検証する。</summary>
    [AvaloniaFact]
    internal void SetImage_画像の差し替えを記録する()
    {
        var logger = new RecordingLogger();
        var original = ThumbnailLogMessage.Prefix;

        try
        {
            var control = new ThumbnailControl { Log = logger, LogPrefix = "[Test]" };
            control.Add(null, "a");
            logger.Messages.Clear();

            control.SetImage(0, CreateBitmap(8, 6));
            control.SetImage(0, null);
        }
        finally
        {
            ThumbnailLogMessage.Prefix = original;
        }

        Assert.Equal(
            ["[Test] Setting: Image[0] null → 8x6", "[Test] Setting: Image[0] 8x6 → null"],
            logger.Messages);
    }

    /// <summary>テーマが適用され、内容の寸法がスクロール範囲として反映されることを検証する。</summary>
    /// <remarks>
    /// 派生クラス自身の型でテーマを探すと（<see cref="Avalonia.StyledElement.StyleKey"/> を上書きしない場合）
    /// <see cref="ScrollViewer"/> のテンプレートが見つからず、内容が表示されずスクロールもできない
    /// （<c>Extent</c> と <c>Viewport</c> が 0 のままになる）。 テーマを当てたうえで寸法を確かめる。
    /// </remarks>
    [AvaloniaFact]
    internal void テーマの適用で内容の寸法がスクロール範囲になる()
    {
        var application = Application.Current!;
        if (!application.Styles.OfType<FluentTheme>().Any())
        {
            application.Styles.Add(new FluentTheme());
        }

        var control = new ThumbnailControl { ImageWidth = 96 };
        for (var i = 0; i < 40; i++)
        {
            control.Add(null, "item" + i);
        }

        var window = new Window { Width = 400, Height = 300, Content = control };
        window.Show();
        window.UpdateLayout();

        Assert.Equal(400, control.Bounds.Width);
        Assert.True(control.Viewport.Height > 0, $"Viewport={control.Viewport}");
        Assert.True(control.Extent.Height > control.Viewport.Height, $"Extent={control.Extent} Viewport={control.Viewport}");
        Assert.False(control.SlotBounds(0).IsEmpty);

        window.Close();
    }
    /// <summary>null のロガーを設定しても例外にしないことを検証する。</summary>
    [AvaloniaFact]
    internal void Log_nullを設定しても例外にしない()
    {
        var control = new ThumbnailControl { Log = null! };

        control.Add(null, "a");

        Assert.Equal(1, control.ItemCount);
    }
}
