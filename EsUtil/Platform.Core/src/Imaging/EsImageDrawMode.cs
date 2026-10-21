namespace EsUtil.Platform.Core.Imaging;

/// <summary>画像を描画先の領域へ配置する方法。</summary>
/// <remarks>
/// ブール値の引数（<c>uniform</c>）で受けると、呼び出し側で <c>false</c> が「引き伸ばし」であることを
/// 読み取れないため、意図が名前で分かる列挙型にする。<br/>
/// <b>本層に置く理由</b>: 配置の方法は UI に依存しない（各 UI 版が同じ値を使う）。 UI ごとに複製すると、
/// 片方だけ値が増えたときに意味が食い違う。
/// </remarks>
public enum EsImageDrawMode
{
    /// <summary>縦横比を無視して領域全体へ引き伸ばす。</summary>
    Fill,

    /// <summary>縦横比を保ち、はみ出さない倍率で中央へ配置する（余白は描画しない）。</summary>
    Uniform,
}
