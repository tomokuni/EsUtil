using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;

namespace EsUtil.Ui.AvaloniaUI.Annotation.Sample;

/// <summary>注釈レイヤーのサンプル ウィンドウ（動作と外観を確認する）。</summary>
/// <remarks>
/// <b>確認できること</b>: 枠線の追加・吹き出しの追加・選択（8 方向のハンドル）・移動・サイズ変更・
/// しっぽの先端のドラッグ・**本文のその場編集**（ダブルクリックまたは Enter）・文字単位の書式・元に戻す。<br/>
/// <b>保存は行わない</b>: 保存（注釈 JSON と合成画像）はホストの仕事であり、本サンプルは表示だけを見る。
/// </remarks>
internal sealed class MainWindow : Window
{
    /// <summary>サンプル画像の幅（ピクセル）。</summary>
    private const int ImageWidth = 960;

    /// <summary>サンプル画像の高さ（ピクセル）。</summary>
    private const int ImageHeight = 640;

    /// <summary>本文の表示・その場編集と書式バーをまとめたコントロール。</summary>
    private readonly AnnotationTextEditor _editor = new();

    /// <summary>注釈を重ねるレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>サンプル ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        Title = "Annotation サンプル";
        Width = 1420;
        Height = 900;

        _layer = _editor.Layer;

        var bitmap = CreateSampleImage();
        _layer.Source = bitmap;
        _layer.SourcePixelWidth = ImageWidth;
        _layer.SourcePixelHeight = ImageHeight;
        var imageSize = new EsSize(ImageWidth, ImageHeight);
        _layer.Document = new AnnotationDocument(ImageWidth, ImageHeight, "sample.png")
            .Add(AnnotationGeometry.CreateFrame(
                new EsRect(60, 60, 340, 220), AnnotationDefaults.FrameAppearance, imageSize))
            .Add(AnnotationGeometry.CreateBubble(
                new EsRect(520, 340, 380, 170),
                new RichText([
                    new TextRun("ここが重要です。", AnnotationDefaults.TextStyle),
                    new TextRun("文字単位の書式", AnnotationDefaults.TextStyle with { IsBold = true, Color = System.Drawing.Color.FromArgb(255, 200, 0, 0) }),
                    new TextRun("は右クリックで編集できます。", AnnotationDefaults.TextStyle),
                ]),
                AnnotationDraftDefaults.Default,
                imageSize,
                new BubbleTail(new EsPoint(430, 200), BubbleEdge.Top, 0.3, 26)));

        var canvas = new Canvas { Background = Brushes.LightGray };
        canvas.Children.Add(_editor);
        _editor.Width = ImageWidth;
        _editor.Height = ImageHeight;
        Canvas.SetLeft(_editor, 8);
        Canvas.SetTop(_editor, 8);

        var settings = new ScrollViewer
        {
            Content = new SettingsPane(_editor),
            Width = 344,
            Background = new SolidColorBrush(Color.FromArgb(255, 248, 248, 248)),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };

        var root = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        DockPanel.SetDock(settings, Dock.Left);
        root.Children.Add(settings);
        root.Children.Add(new ScrollViewer { Content = canvas });
        Content = root;
    }

    /// <summary>サンプル画像（市松模様）を生成する。</summary>
    /// <returns>表示用の画像。</returns>
    /// <remarks>注釈の視認性を確かめるため、明暗がはっきりした背景にする。</remarks>
    private static IImage CreateSampleImage()
    {
        var bitmap = EsUtil.Platform.Core.Imaging.EsBitmap.CreateFilled(ImageWidth, ImageHeight, span =>
        {
            for (var y = 0; y < ImageHeight; y++)
            {
                for (var x = 0; x < ImageWidth; x++)
                {
                    var checker = ((x / 80) + (y / 80)) % 2 == 0;
                    var offset = (y * ImageWidth + x) * 4;
                    span[offset] = (byte)(checker ? 200 : 120);
                    span[offset + 1] = (byte)(checker ? 190 : 140);
                    span[offset + 2] = (byte)(checker ? 170 : 160);
                    span[offset + 3] = 255;
                }
            }
        });

        return EsBitmapAdapter.Adopt(bitmap);
    }

    /// <summary>ツールバーを組み立てる。</summary>
    /// <returns>ツールバー。</returns>
    private Control BuildToolbar()
    {
        var addFrame = new Button { Content = "枠線を追加" };
        addFrame.Click += (_, _) => _layer.AddFrame(new EsRect(120, 320, 300, 200));

        var addBubble = new Button { Content = "吹き出しを追加" };
        addBubble.Click += (_, _) => _layer.AddBubble(new EsRect(300, 80, 320, 140), "新しい吹き出し");

        var edit = new Button { Content = "本文を編集" };
        edit.Click += (_, _) => _layer.BeginTextEditing();

        var remove = new Button { Content = "削除" };
        remove.Click += (_, _) => _layer.DeleteSelected();

        var undo = new Button { Content = "元に戻す" };
        undo.Click += (_, _) => _layer.Undo();

        var redo = new Button { Content = "やり直す" };
        redo.Click += (_, _) => _layer.Redo();

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 6, 8, 6),
        };
        bar.Children.Add(addFrame);
        bar.Children.Add(addBubble);
        bar.Children.Add(edit);
        bar.Children.Add(remove);
        bar.Children.Add(undo);
        bar.Children.Add(redo);
        bar.Children.Add(new TextBlock
        {
            Text = "右クリックで外観・書式を編集 / ダブルクリックで本文をその場編集（選択した文字は書式バーで変更）/ Delete で削除 / 方向キーで移動 / Ctrl+Z で元に戻す",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Opacity = 0.7,
        });

        return bar;
    }
}
