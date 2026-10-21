namespace EsUtil.Platform.Core.Svg;

/// <summary>SVG アイコンの内容（UI 非依存の形）。</summary>
/// <param name="FileName">SVG ファイル名。</param>
/// <param name="ViewBoxWidth">viewBox の幅（通常 24）。</param>
/// <param name="ViewBoxHeight">viewBox の高さ（通常 24）。</param>
/// <param name="PathData">パス データ（<c>d</c> 属性。 描画順）。</param>
/// <param name="FillArgb">塗りの色（<c>0xAARRGGBB</c>）。</param>
/// <remarks>
/// <b>ここまでが UI 非依存の部分</b>である: SVG の読み込みと解析は本プロジェクトが単一所有し、<br/>
/// 各 UI 版はパス データを自分の描画 API（Avalonia の <c>StreamGeometry</c>・Skia の <c>SKPath</c>・<br/>
/// GDI+ の <c>GraphicsPath</c>・WinUI 3 の <c>Geometry</c>）へ変換して描くだけにする。<br/>
/// <b>色は描画側で置き換える</b>: アイコンの色はテーマに依存するため、各 UI 版は <see cref="FillArgb"/> を<br/>
/// 既定値として使い、テーマ色が決まっている場合はその色で塗る。
/// </remarks>
public readonly record struct SvgIcon(
    string FileName,
    double ViewBoxWidth,
    double ViewBoxHeight,
    IReadOnlyList<string> PathData,
    uint FillArgb);