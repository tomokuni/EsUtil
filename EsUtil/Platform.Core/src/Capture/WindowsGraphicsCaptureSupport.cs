using Windows.Foundation.Metadata;

namespace EsUtil.Platform.Core.Capture;

/// <summary>Windows.Graphics.Capture（WGC）を実行中の環境で使えるかを返す。</summary>
/// <remarks>
/// <b>何のためか</b>: キャプチャの方式（GDI の <c>BitBlt</c> と WGC）を利用者に選ばせるとき、
/// <b>使えない環境では選択肢を無効にする</b>必要がある。 本クラスはその判定だけを持ち、撮影そのものは行わない。<br/>
/// <br/>
/// <b>OS の版と API の有無を両方見る</b>: WGC の API 面は Windows SDK の射影の版で決まり、
/// <c>IsCursorCaptureEnabled</c>（1903）と <c>IsBorderRequired</c>（Windows 11）は
/// <b>対象フレームワーク 17763 の射影には存在しない</b>（実測 2026-11-14: 18362 の射影にも無く、20348 で初めて現れる）。
/// そのため本クラスは版だけで判断せず、<see cref="ApiInformation"/> で型・プロパティの実在も確かめる
/// （文字列で問い合わせるため、射影に無い API でも判定できる）。<br/>
/// <br/>
/// <b>true は「使える見込み」である</b>: 実際の撮影は GPU とセッションの状態にも依存するため、
/// 撮影時の失敗に備えて呼び出し側は GDI へ退避できるようにする（判定と撮影の責務を分ける）。
/// </remarks>
public static class WindowsGraphicsCaptureSupport
{
    /// <summary>WGC の型名（<c>ApiInformation</c> への問い合わせ用）。</summary>
    private const string ItemType = "Windows.Graphics.Capture.GraphicsCaptureItem";

    /// <summary>セッションの型名（<c>ApiInformation</c> への問い合わせ用）。</summary>
    private const string SessionType = "Windows.Graphics.Capture.GraphicsCaptureSession";

    /// <summary>画面（モニター）の撮影ができるかを返す。</summary>
    /// <value>Windows 10 1809（ビルド 17763）以降で、かつ WGC の型が実在する場合は true。</value>
    /// <remarks>API 自体は 1803 で追加されたが、本リポジトリの実装が使うフレーム プール（<c>CreateFreeThreaded</c>）が 1809 のため、1809 を下限にする。</remarks>
    public static bool IsDisplayCaptureSupported { get; } = Check(10, 0, 17763, () => ApiInformation.IsTypePresent(ItemType));

    /// <summary>ウィンドウの撮影ができるかを返す。</summary>
    /// <value>Windows 10 1809（ビルド 17763）以降で、かつ WGC の型が実在する場合は true。</value>
    /// <remarks>ウィンドウの撮影（<c>CreateFromWindow</c>）は 1809 で追加された（それ以前は <c>CreateFromVisual</c> だけ）。</remarks>
    public static bool IsWindowCaptureSupported { get; } = Check(10, 0, 17763, () => ApiInformation.IsTypePresent(ItemType));

    /// <summary>撮影へカーソルを含めるかを切り替えられるかを返す。</summary>
    /// <value>Windows 10 1903（ビルド 18362）以降で、かつ <c>IsCursorCaptureEnabled</c> が実在する場合は true。</value>
    /// <remarks>
    /// false の場合、WGC で撮影すると<b>カーソルは常に含まれる</b>（切り替えられない）。
    /// カーソルを外したいときは GDI の <c>BitBlt</c> を使う（自前で合成するため制御できる）。
    /// </remarks>
    public static bool IsCursorCaptureSupported { get; } = Check(
        10, 0, 18362, () => ApiInformation.IsPropertyPresent(SessionType, "IsCursorCaptureEnabled"));

    /// <summary>撮影中の枠（黄色い枠）の表示を消せるかを返す。</summary>
    /// <value>Windows 11（ビルド 20348）以降で、かつ <c>IsBorderRequired</c> が実在する場合は true。</value>
    /// <remarks>
    /// false の場合、WGC で撮影すると<b>対象の周囲に枠が描かれる</b>（OS が「撮影中」を示すため）。
    /// GDI の <c>BitBlt</c> は枠を描かないため、枠を避けたいときは GDI を選ぶ。
    /// </remarks>
    public static bool IsBorderControlSupported { get; } = Check(
        10, 0, 20348, () => ApiInformation.IsPropertyPresent(SessionType, "IsBorderRequired"));

    /// <summary>WGC を使えるかを返す（画面の撮影ができるかと同じ判定）。</summary>
    /// <value>WGC を選択肢として提示できる場合は true。</value>
    /// <remarks>ウィンドウの撮影まで必要なら <see cref="IsWindowCaptureSupported"/> も確かめる。</remarks>
    public static bool IsSupported => IsDisplayCaptureSupported;

    /// <summary>撮影の方式を選べない理由を返す。</summary>
    /// <returns>利用者へ見せる理由。 使える場合は <see cref="string.Empty"/>。</returns>
    /// <remarks>
    /// 設定画面で選択肢を無効にするときの説明に使う（「なぜ選べないか」を書かないと、
    /// 利用者は不具合だと受け取る）。 文言は本メソッドが単一所有する（4 つの UI 版で揃える）。
    /// </remarks>
    public static string DescribeUnsupportedReason()
    {
        if (!IsDisplayCaptureSupported)
        {
            return "この環境では Windows.Graphics.Capture を使えません（Windows 10 1809 以降が必要です）";
        }

        if (!IsWindowCaptureSupported)
        {
            return "この環境ではウィンドウの撮影に Windows.Graphics.Capture を使えません（Windows 10 1809 以降が必要です）";
        }

        return string.Empty;
    }

    /// <summary>OS の版と API の実在を確かめる。</summary>
    /// <param name="major">必要な OS のメジャー バージョン。</param>
    /// <param name="minor">必要な OS のマイナー バージョン。</param>
    /// <param name="build">必要な OS のビルド番号。</param>
    /// <param name="isApiPresent">API が実在するかを返す検査。</param>
    /// <returns>両方を満たす場合は true。</returns>
    /// <remarks>
    /// <b>版を先に見る</b>: 古い OS で <see cref="ApiInformation"/> を呼ぶと、型が無い場合に例外になることがあるため、
    /// 版で足切りしてから問い合わせる。 問い合わせ自体が失敗した場合も false にする（診断のための判定でアプリを止めない）。
    /// </remarks>
    private static bool Check(int major, int minor, int build, Func<bool> isApiPresent)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(major, minor, build))
        {
            return false;
        }

        try
        {
            return isApiPresent();
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 問い合わせできない環境では「使えない」として扱う（選ばせない方が安全）
            return false;
        }
    }
}
