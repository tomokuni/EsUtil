using Microsoft.Extensions.Logging;
using Xunit;

namespace EsUtil.Ui.WinForms.Thumbnail.Tests;

/// <summary>サムネイル一覧（WinForms 版）の公開 API とログの単体テスト。</summary>
/// <remarks>
/// コントロールの生成とプロパティ設定・公開メソッドのみを検証するため、ウィンドウ表示を必要としない。<br/>
/// 入力（マウス）はウィンドウを必要とするため、公開 API 経由で検証できる範囲に限定する。<br/>
/// 画像の所有権はコントロールへ移るため、テストからは解放しない（コントロールの破棄で解放される）。
/// </remarks>
[Collection(ThumbnailLogPrefix.Name)]
public class ThumbnailControlTests
{
    /// <summary>指定サイズの画像を生成する。</summary>
    /// <param name="width">幅（ピクセル）。</param>
    /// <param name="height">高さ（ピクセル）。</param>
    /// <returns>生成した画像。 所有権は <see cref="ThumbnailControl"/> へ移す。</returns>
    private static Bitmap CreateBitmap(int width, int height) => new(width, height);

    /// <summary>項目を末尾へ追加し、追加した添字を返すことを検証する。</summary>
    [Fact]
    internal void Add_項目を末尾へ追加する()
    {
        using var control = new ThumbnailControl();

        Assert.Equal(0, control.Add(null, "a"));
        Assert.Equal(1, control.Add(null, "b"));
        Assert.Equal(2, control.ItemCount);
    }

    /// <summary>選択の設定と範囲外の解除を検証する。</summary>
    [Fact]
    internal void SelectedIndex_範囲外は選択を解除する()
    {
        using var control = new ThumbnailControl();
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
    [Fact]
    internal void ClearItems_項目を空にする()
    {
        using var control = new ThumbnailControl();
        control.Add(CreateBitmap(8, 8), "a");
        control.Add(CreateBitmap(8, 8), "b");

        control.ClearItems();

        Assert.Equal(0, control.ItemCount);
    }

    /// <summary>範囲外の添字では何もしないことを検証する。</summary>
    [Fact]
    internal void SetImage_範囲外の添字では何もしない()
    {
        using var control = new ThumbnailControl();
        control.Add(null, "a");

        control.SetImage(-1, null);
        control.SetImage(5, null);
        control.SetCaption(5, "x");

        Assert.Equal(1, control.ItemCount);
    }

    /// <summary>レイアウト後の項目の矩形が内容の座標で返ることを検証する。</summary>
    [Fact]
    internal void ItemBounds_レイアウト後に矩形を返す()
    {
        using var control = new ThumbnailControl { Width = 400, Height = 300 };
        control.Add(null, "a");

        control.RefreshLayout();

        var bounds = control.ItemBounds(0);
        Assert.True(bounds.Width > 0);
        Assert.True(bounds.Height > 0);
        Assert.Equal(Rectangle.Empty, control.ItemBounds(5));
    }

    /// <summary>項目数の変更がログへ記録されることを検証する。</summary>
    [Fact]
    internal void Add_と_ClearItems_は項目数を記録する()
    {
        var logger = new RecordingLogger();
        var original = ThumbnailLogMessage.Prefix;

        try
        {
            using var control = new ThumbnailControl { Log = logger, LogPrefix = "[Test]" };
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
    [Fact]
    internal void SetImage_画像の差し替えを記録する()
    {
        var logger = new RecordingLogger();
        var original = ThumbnailLogMessage.Prefix;

        try
        {
            using var control = new ThumbnailControl { Log = logger, LogPrefix = "[Test]" };
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

    /// <summary>null のロガーを設定しても例外にしないことを検証する。</summary>
    [Fact]
    internal void Log_nullを設定しても例外にしない()
    {
        using var control = new ThumbnailControl { Log = null! };

        control.Add(null, "a");

        Assert.Equal(1, control.ItemCount);
    }
}
