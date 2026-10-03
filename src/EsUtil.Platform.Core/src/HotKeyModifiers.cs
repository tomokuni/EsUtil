namespace EsUtil.Platform.Core;

/// <summary>ホットキーの修飾キーを指定するためのフラグ列挙型。</summary>
/// <remarks>
/// ビットフラグとして設計されており、複数の修飾キーを組み合わせて使用する。<br/>
/// <see cref="HotKeySetting"/> と組み合わせてホットキーの一意な組合せを定義する。<br/>
/// 値は Win32 RegisterHotKey の MOD_ 定数（MOD_ALT=0x1 / MOD_CONTROL=0x2 / MOD_SHIFT=0x4 / MOD_WIN=0x8）と一致する。
/// </remarks>
[Flags]
public enum HotKeyModifiers
{
    /// <summary>修飾キーなし</summary>
    None = 0,

    /// <summary>Alt キー</summary>
    Alt = 0x0001,

    /// <summary>Ctrl キー</summary>
    Control = 0x0002,

    /// <summary>Shift キー</summary>
    Shift = 0x0004,

    /// <summary>Windows キー</summary>
    Windows = 0x0008,
}
