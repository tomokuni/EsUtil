using EsUtil.Platform.Core.Native.Win;

namespace EsUtil.Ui.WinForms.Interop;

/// <summary>指定位置のモニターの表示倍率を照会する（WinForms 版）。</summary>
/// <remarks>
/// <b>なぜ本プロジェクトが持つか</b>: WinForms の公開 API は「モニター単位の DPI」を公開しない
/// （<c>Screen</c> は矩形とデバイス名のみ、<c>Control.DeviceDpi</c> はコントロールが属するモニターの 1 値のみ）。
/// 仮想スクリーン全体を 1 枚の画像として扱う撮影セッションは、選択矩形が属するモニターの表示倍率を
/// 位置から解決する必要がある（サイズラベルの表示倍率）。<br/>
/// <b>照会そのものはコアが単一所有する</b>（<see cref="WinMonitorDpi.GetEffectiveDpi"/>。 4 つの UI 版が同じ値を使う）。
/// 本クラスは WinForms の利用側へ<b>スケール係数（1.0 = 96 DPI）</b>で渡すための変換だけを持つ。<br/>
/// <b>照会できない場合</b>: 0 を返す（<see cref="DpiScaler.IsValidScaling"/> が false。 呼び出し側が代替値で解決する。
/// 例外にしない）。
/// </remarks>
public static class MonitorDpi
{
    /// <summary>指定位置にあるモニターの DPI スケール係数を照会する。</summary>
    /// <param name="point">照会する位置（画面物理ピクセル）。</param>
    /// <returns>スケール係数（1.0 = 96 DPI）。 照会できない場合は 0（無効値。<see cref="DpiScaler.IsValidScaling"/> が false）。</returns>
    /// <remarks>
    /// <see cref="DpiScaler.ToScaling(double)"/> で DPI を係数へ換算する（換算規則はコアが単一所有する）。<br/>
    /// 戻り値を 0 とするのは「照会できなかった」を呼び出し側が判別できるようにするためである<br/>
    /// （1.0 を返すと 96 DPI のモニターと区別できない）。
    /// </remarks>
    public static double GetScaleForPoint(EsPointInt point)
        => WinMonitorDpi.GetEffectiveDpi(point) is uint dpi ? DpiScaler.ToScaling(dpi) : 0;
}
