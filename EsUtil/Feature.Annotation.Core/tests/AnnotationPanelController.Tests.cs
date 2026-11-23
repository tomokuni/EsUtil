using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Annotation.Core.Tests;

/// <summary><see cref="AnnotationPanelController"/> の開閉とドラッグを検証する。</summary>
public sealed class AnnotationPanelControllerTests
{
    /// <summary>表示するとホストへ作成が伝わり、位置はアンカーのままになる。</summary>
    [Fact]
    internal void Show_ホストへ作成を伝える()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.Show(AnnotationPanelKind.StyleEditor, new EsPoint(640, 480));

        Assert.True(controller.IsOpen);
        Assert.Equal(AnnotationPanelKind.StyleEditor, controller.Kind);
        Assert.Equal(1, host.CreateCount);
        Assert.Equal(new EsPoint(640, 480), host.LastCreatePosition);
        Assert.Equal(AnnotationPanelAppearance.StyleEditor, host.LastAppearance);
    }

    /// <summary>対象に被せない位置へ出す（本文の外へ置く）。</summary>
    [Fact]
    internal void ShowOutside_対象の外へ出す()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.ShowOutside(
            AnnotationPanelKind.FormatBar,
            target: new EsRect(400, 500, 200, 100),
            panelSize: new EsSize(300, 40),
            gap: 8);

        Assert.True(controller.IsOpen);
        Assert.Equal(400, host.LastCreatePosition.X);
        Assert.Equal(500 - 8 - 40, host.LastCreatePosition.Y);
    }

    /// <summary>閉じるとホストへ破棄が伝わる（2 回目は何もしない）。</summary>
    [Fact]
    internal void Close_ホストへ破棄を伝える()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.FormatBar, new EsPoint(1, 2));

        controller.Close();
        controller.Close();

        Assert.False(controller.IsOpen);
        Assert.Null(controller.Kind);
        Assert.Equal(1, host.DestroyCount);
    }

    /// <summary>ドラッグでは押下時からのポインターの移動量だけをホストへ渡す。</summary>
    [Fact]
    internal void DragTo_移動量だけをホストへ渡す()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.StyleEditor, new EsPoint(100, 200));

        controller.BeginDrag(new EsPoint(110, 210));
        controller.DragTo(new EsPoint(130, 240));

        Assert.Equal(new EsPoint(120, 230), host.LastMovePosition);
        Assert.True(controller.IsDragging);

        controller.EndDrag();
        Assert.False(controller.IsDragging);
    }

    /// <summary>表示していないときのドラッグ操作は無視する。</summary>
    [Fact]
    internal void DragTo_表示していないときは動かさない()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);

        controller.BeginDrag(new EsPoint(0, 0));
        controller.DragTo(new EsPoint(50, 50));

        Assert.False(controller.IsDragging);
        Assert.Equal(0, host.MoveCount);
    }

    /// <summary>パネルの外側を押すと閉じ、内側を押しても閉じない。</summary>
    [Fact]
    internal void CloseIfOutside_外側の押下だけで閉じる()
    {
        var host = new HostSpy();
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.StyleEditor, new EsPoint(100, 200));

        Assert.False(controller.CloseIfOutside(new EsPoint(120, 220)));
        Assert.True(controller.IsOpen);

        Assert.True(controller.CloseIfOutside(new EsPoint(20, 20)));
        Assert.False(controller.IsOpen);
        Assert.Equal(1, host.DestroyCount);
    }

    /// <summary>実寸が置ける領域からはみ出す場合は内側へ収める。</summary>
    [Fact]
    internal void Constrain_はみ出す分だけ内側へ収める()
    {
        var host = new HostSpy { AvailableArea = new EsRect(100, 50, 400, 300), PanelSize = new EsSize(300, 120) };
        var controller = new AnnotationPanelController(host);
        controller.Show(AnnotationPanelKind.StyleEditor, new EsPoint(480, 380));

        controller.Constrain(new EsSize(300, 120));

        Assert.Equal(new EsPoint(200, 230), host.LastMovePosition);
        Assert.Equal(1, host.MoveCount);
    }

    /// <summary>ホストの呼び出しを記録するスパイ。</summary>
    private sealed class HostSpy : IAnnotationPanelHost
    {
        /// <summary>作成の回数。</summary>
        public int CreateCount { get; private set; }

        /// <summary>移動の回数。</summary>
        public int MoveCount { get; private set; }

        /// <summary>破棄の回数。</summary>
        public int DestroyCount { get; private set; }

        /// <summary>最後に作成を頼まれた位置。</summary>
        public EsPoint LastCreatePosition { get; private set; }

        /// <summary>最後に渡された外観。</summary>
        public AnnotationPanelAppearance? LastAppearance { get; private set; }

        /// <summary>最後に移動を頼まれた位置。</summary>
        public EsPoint LastMovePosition { get; private set; }

        /// <summary>パネルを置ける領域。</summary>
        public EsRect AvailableArea { get; set; } = new(0, 0, 1280, 800);

        /// <summary>パネルの大きさ。</summary>
        public EsSize PanelSize { get; set; } = new(300, 120);

        /// <summary>種別ごとのパネルの左上（作成・移動で更新する）。</summary>
        private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

        /// <inheritdoc/>
        public EsPoint GetPanelPosition(AnnotationPanelKind kind)
            => _positions.TryGetValue(kind, out var position) ? position : default;

        /// <inheritdoc/>
        public EsSize GetPanelSize(AnnotationPanelKind kind)
        {
            _ = kind;
            return PanelSize;
        }

        /// <inheritdoc/>
        public void CreatePanel(
            AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
        {
            CreateCount++;
            LastAppearance = appearance;
            LastCreatePosition = position;
            _positions[kind] = position;
        }

        /// <inheritdoc/>
        public void MovePanel(AnnotationPanelKind kind, EsPoint position)
        {
            MoveCount++;
            LastMovePosition = position;
            _positions[kind] = position;
        }

        /// <inheritdoc/>
        public void DestroyPanel(AnnotationPanelKind kind)
        {
            _ = kind;
            DestroyCount++;
        }
    }
}
