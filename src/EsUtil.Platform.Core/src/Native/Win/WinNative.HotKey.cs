using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

// 本ファイルはグローバル ホットキー（user32.dll）の宣言とキー変換を集約する。 型の説明は WinNative.cs を参照する。
internal static partial class WinNative
{

    // ============================================================
    // 定数（ホットキー）
    // ============================================================

    /// <summary>WM_HOTKEY: グローバルホットキーが押下されたときに送られるメッセージ。</summary>
    internal const uint WM_HOTKEY = 0x0312;

    // ============================================================
    // user32.dll — ホットキー
    // ============================================================

    /// <summary>グローバルホットキーを登録する（user32.RegisterHotKey）。</summary>
    /// <param name="hWnd">ホットキー通知を受け取るウィンドウのハンドル。</param>
    /// <param name="id">ホットキーの識別子。</param>
    /// <param name="fsModifiers">修飾キーのフラグ（MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN）。</param>
    /// <param name="vk">仮想キーコード。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, uint vk);

    /// <summary>グローバルホットキーを解除する（user32.UnregisterHotKey）。</summary>
    /// <param name="hWnd">ホットキーを登録したウィンドウのハンドル。</param>
    /// <param name="id">解除するホットキーの識別子。</param>
    /// <returns>成功した場合は <see langword="true"/>。</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>UI 非依存の <see cref="EsKey"/> を Win32 仮想キーコードへ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>
    /// 対応する Win32 仮想キーコード。 対応するコードが無いキー（<c>LineFeed</c> / <c>System</c> / <c>DeadCharProcessed</c> /<br/>
    /// <c>Fn*</c> / メディアの一部（<c>MediaHome</c> 等））は <c>0</c>（<c>RegisterHotKey</c> が失敗する値 = 登録できない）。
    /// </returns>
    /// <remarks>
    /// <c>RegisterHotKey</c> の vk パラメータに渡すために使用する。<br/>
    /// <b>対応表に無いキーへ序数（<c>(uint)key</c>）を返さない</b>: 序数は仮想キーコードと別体系であり、<br/>
    /// 無言で<b>別のキーが登録される</b>（実測: 以前は <c>PrintScreen</c> の序数 89 = <c>0x59</c> = <c>Y</c> が登録され、<br/>
    /// <c>Sleep</c> の序数 65 = <c>0x41</c> = <c>A</c> が登録されていた）。 未対応は <c>0</c> として失敗させる（呼び出し側が記録する）。
    /// </remarks>
    internal static uint KeyToWin32VirtualKey(EsKey key) => key switch
    {
        EsKey.None => 0x00,
        EsKey.Cancel => 0x03,
        EsKey.Back => 0x08,
        EsKey.Tab => 0x09,
        EsKey.Clear => 0x0C,
        EsKey.Return => 0x0D,
        EsKey.Enter => 0x0D,
        EsKey.Pause => 0x13,
        EsKey.CapsLock => 0x14,
        EsKey.Capital => 0x14,
        EsKey.HangulMode => 0x15,
        EsKey.KanaMode => 0x15,
        EsKey.JunjaMode => 0x17,
        EsKey.FinalMode => 0x18,
        EsKey.KanjiMode => 0x19,
        EsKey.HanjaMode => 0x19,
        EsKey.Escape => 0x1B,
        EsKey.ImeConvert => 0x1C,
        EsKey.ImeNonConvert => 0x1D,
        EsKey.ImeAccept => 0x1E,
        EsKey.ImeModeChange => 0x1F,
        EsKey.Space => 0x20,
        EsKey.PageUp => 0x21,
        EsKey.Prior => 0x21,
        EsKey.PageDown => 0x22,
        EsKey.Next => 0x22,
        EsKey.End => 0x23,
        EsKey.Home => 0x24,
        EsKey.Left => 0x25,
        EsKey.Up => 0x26,
        EsKey.Right => 0x27,
        EsKey.Down => 0x28,
        EsKey.Select => 0x29,
        EsKey.Print => 0x2A,
        EsKey.Execute => 0x2B,
        EsKey.Snapshot => 0x2C,
        EsKey.PrintScreen => 0x2C,
        EsKey.Insert => 0x2D,
        EsKey.Delete => 0x2E,
        EsKey.Help => 0x2F,
        EsKey.D0 => 0x30,
        EsKey.D1 => 0x31,
        EsKey.D2 => 0x32,
        EsKey.D3 => 0x33,
        EsKey.D4 => 0x34,
        EsKey.D5 => 0x35,
        EsKey.D6 => 0x36,
        EsKey.D7 => 0x37,
        EsKey.D8 => 0x38,
        EsKey.D9 => 0x39,
        EsKey.A => 0x41,
        EsKey.B => 0x42,
        EsKey.C => 0x43,
        EsKey.D => 0x44,
        EsKey.E => 0x45,
        EsKey.F => 0x46,
        EsKey.G => 0x47,
        EsKey.H => 0x48,
        EsKey.I => 0x49,
        EsKey.J => 0x4A,
        EsKey.K => 0x4B,
        EsKey.L => 0x4C,
        EsKey.M => 0x4D,
        EsKey.N => 0x4E,
        EsKey.O => 0x4F,
        EsKey.P => 0x50,
        EsKey.Q => 0x51,
        EsKey.R => 0x52,
        EsKey.S => 0x53,
        EsKey.T => 0x54,
        EsKey.U => 0x55,
        EsKey.V => 0x56,
        EsKey.W => 0x57,
        EsKey.X => 0x58,
        EsKey.Y => 0x59,
        EsKey.Z => 0x5A,
        EsKey.LWin => 0x5B,
        EsKey.RWin => 0x5C,
        EsKey.Apps => 0x5D,
        EsKey.Sleep => 0x5F,
        EsKey.NumPad0 => 0x60,
        EsKey.NumPad1 => 0x61,
        EsKey.NumPad2 => 0x62,
        EsKey.NumPad3 => 0x63,
        EsKey.NumPad4 => 0x64,
        EsKey.NumPad5 => 0x65,
        EsKey.NumPad6 => 0x66,
        EsKey.NumPad7 => 0x67,
        EsKey.NumPad8 => 0x68,
        EsKey.NumPad9 => 0x69,
        EsKey.Multiply => 0x6A,
        EsKey.Add => 0x6B,
        EsKey.Separator => 0x6C,
        EsKey.Subtract => 0x6D,
        EsKey.Decimal => 0x6E,
        EsKey.Divide => 0x6F,
        EsKey.F1 => 0x70,
        EsKey.F2 => 0x71,
        EsKey.F3 => 0x72,
        EsKey.F4 => 0x73,
        EsKey.F5 => 0x74,
        EsKey.F6 => 0x75,
        EsKey.F7 => 0x76,
        EsKey.F8 => 0x77,
        EsKey.F9 => 0x78,
        EsKey.F10 => 0x79,
        EsKey.F11 => 0x7A,
        EsKey.F12 => 0x7B,
        EsKey.F13 => 0x7C,
        EsKey.F14 => 0x7D,
        EsKey.F15 => 0x7E,
        EsKey.F16 => 0x7F,
        EsKey.F17 => 0x80,
        EsKey.F18 => 0x81,
        EsKey.F19 => 0x82,
        EsKey.F20 => 0x83,
        EsKey.F21 => 0x84,
        EsKey.F22 => 0x85,
        EsKey.F23 => 0x86,
        EsKey.F24 => 0x87,
        EsKey.NumLock => 0x90,
        EsKey.Scroll => 0x91,
        EsKey.LeftShift => 0xA0,
        EsKey.RightShift => 0xA1,
        EsKey.LeftCtrl => 0xA2,
        EsKey.RightCtrl => 0xA3,
        EsKey.LeftAlt => 0xA4,
        EsKey.RightAlt => 0xA5,
        EsKey.BrowserBack => 0xA6,
        EsKey.BrowserForward => 0xA7,
        EsKey.BrowserRefresh => 0xA8,
        EsKey.BrowserStop => 0xA9,
        EsKey.BrowserSearch => 0xAA,
        EsKey.BrowserFavorites => 0xAB,
        EsKey.BrowserHome => 0xAC,
        EsKey.VolumeMute => 0xAD,
        EsKey.VolumeDown => 0xAE,
        EsKey.VolumeUp => 0xAF,
        EsKey.MediaNextTrack => 0xB0,
        EsKey.MediaPreviousTrack => 0xB1,
        EsKey.MediaStop => 0xB2,
        EsKey.MediaPlayPause => 0xB3,
        EsKey.LaunchMail => 0xB4,
        EsKey.SelectMedia => 0xB5,
        EsKey.LaunchApplication1 => 0xB6,
        EsKey.LaunchApplication2 => 0xB7,
        EsKey.OemSemicolon => 0xBA,
        EsKey.Oem1 => 0xBA,
        EsKey.OemPlus => 0xBB,
        EsKey.OemComma => 0xBC,
        EsKey.OemMinus => 0xBD,
        EsKey.OemPeriod => 0xBE,
        EsKey.OemQuestion => 0xBF,
        EsKey.Oem2 => 0xBF,
        EsKey.OemTilde => 0xC0,
        EsKey.Oem3 => 0xC0,
        EsKey.AbntC1 => 0xC1,
        EsKey.AbntC2 => 0xC2,
        EsKey.OemOpenBrackets => 0xDB,
        EsKey.Oem4 => 0xDB,
        EsKey.OemPipe => 0xDC,
        EsKey.Oem5 => 0xDC,
        EsKey.OemCloseBrackets => 0xDD,
        EsKey.Oem6 => 0xDD,
        EsKey.OemQuotes => 0xDE,
        EsKey.Oem7 => 0xDE,
        EsKey.Oem8 => 0xDF,
        EsKey.OemBackslash => 0xE2,
        EsKey.Oem102 => 0xE2,
        EsKey.ImeProcessed => 0xE5,
        EsKey.OemAttn => 0xF0,
        EsKey.DbeAlphanumeric => 0xF0,
        EsKey.OemFinish => 0xF1,
        EsKey.DbeKatakana => 0xF1,
        EsKey.DbeHiragana => 0xF2,
        EsKey.OemCopy => 0xF2,
        EsKey.DbeSbcsChar => 0xF3,
        EsKey.OemAuto => 0xF3,
        EsKey.DbeDbcsChar => 0xF4,
        EsKey.OemEnlw => 0xF4,
        EsKey.OemBackTab => 0xF5,
        EsKey.DbeRoman => 0xF5,
        EsKey.DbeNoRoman => 0xF6,
        EsKey.Attn => 0xF6,
        EsKey.CrSel => 0xF7,
        EsKey.DbeEnterWordRegisterMode => 0xF7,
        EsKey.ExSel => 0xF8,
        EsKey.DbeEnterImeConfigureMode => 0xF8,
        EsKey.EraseEof => 0xF9,
        EsKey.DbeFlushString => 0xF9,
        EsKey.Play => 0xFA,
        EsKey.DbeCodeInput => 0xFA,
        EsKey.DbeNoCodeInput => 0xFB,
        EsKey.Zoom => 0xFB,
        EsKey.NoName => 0xFC,
        EsKey.DbeDetermineString => 0xFC,
        EsKey.DbeEnterDialogConversionMode => 0xFD,
        EsKey.Pa1 => 0xFD,
        EsKey.OemClear => 0xFE,
        _ => 0
    };
}
