namespace EsUtil.Platform.Core;

/// <summary>UI フレームワーク非依存のキー識別子。</summary>
/// <remarks>
/// メンバー名は Avalonia.Input.Key と同一にし、ホットキー設定の文字列表現・シリアライズの互換性を保つ。<br/>
/// 各 UI 実装（Avalonia/MewUI）は、自身のキー型との相互変換を Interop 層で提供する。<br/>
/// </remarks>
public enum EsKey
{
    /// <summary>None キー。</summary>
    None,

    /// <summary>Cancel キー。</summary>
    Cancel,

    /// <summary>Back キー。</summary>
    Back,

    /// <summary>Tab キー。</summary>
    Tab,

    /// <summary>LineFeed キー。</summary>
    LineFeed,

    /// <summary>Clear キー。</summary>
    Clear,

    /// <summary>Return キー。</summary>
    Return,

    /// <summary>Enter キー。</summary>
    Enter,

    /// <summary>Pause キー。</summary>
    Pause,

    /// <summary>CapsLock キー。</summary>
    CapsLock,

    /// <summary>Capital キー。</summary>
    Capital,

    /// <summary>HangulMode キー。</summary>
    HangulMode,

    /// <summary>KanaMode キー。</summary>
    KanaMode,

    /// <summary>JunjaMode キー。</summary>
    JunjaMode,

    /// <summary>FinalMode キー。</summary>
    FinalMode,

    /// <summary>KanjiMode キー。</summary>
    KanjiMode,

    /// <summary>HanjaMode キー。</summary>
    HanjaMode,

    /// <summary>Escape キー。</summary>
    Escape,

    /// <summary>ImeConvert キー。</summary>
    ImeConvert,

    /// <summary>ImeNonConvert キー。</summary>
    ImeNonConvert,

    /// <summary>ImeAccept キー。</summary>
    ImeAccept,

    /// <summary>ImeModeChange キー。</summary>
    ImeModeChange,

    /// <summary>Space キー。</summary>
    Space,

    /// <summary>PageUp キー。</summary>
    PageUp,

    /// <summary>Prior キー。</summary>
    Prior,

    /// <summary>PageDown キー。</summary>
    PageDown,

    /// <summary>Next キー。</summary>
    Next,

    /// <summary>End キー。</summary>
    End,

    /// <summary>Home キー。</summary>
    Home,

    /// <summary>Left キー。</summary>
    Left,

    /// <summary>Up キー。</summary>
    Up,

    /// <summary>Right キー。</summary>
    Right,

    /// <summary>Down キー。</summary>
    Down,

    /// <summary>Select キー。</summary>
    Select,

    /// <summary>Print キー。</summary>
    Print,

    /// <summary>Execute キー。</summary>
    Execute,

    /// <summary>Snapshot キー。</summary>
    Snapshot,

    /// <summary>PrintScreen キー。</summary>
    PrintScreen,

    /// <summary>Insert キー。</summary>
    Insert,

    /// <summary>Delete キー。</summary>
    Delete,

    /// <summary>Help キー。</summary>
    Help,

    /// <summary>D0 キー。</summary>
    D0,

    /// <summary>D1 キー。</summary>
    D1,

    /// <summary>D2 キー。</summary>
    D2,

    /// <summary>D3 キー。</summary>
    D3,

    /// <summary>D4 キー。</summary>
    D4,

    /// <summary>D5 キー。</summary>
    D5,

    /// <summary>D6 キー。</summary>
    D6,

    /// <summary>D7 キー。</summary>
    D7,

    /// <summary>D8 キー。</summary>
    D8,

    /// <summary>D9 キー。</summary>
    D9,

    /// <summary>A キー。</summary>
    A,

    /// <summary>B キー。</summary>
    B,

    /// <summary>C キー。</summary>
    C,

    /// <summary>D キー。</summary>
    D,

    /// <summary>E キー。</summary>
    E,

    /// <summary>F キー。</summary>
    F,

    /// <summary>G キー。</summary>
    G,

    /// <summary>H キー。</summary>
    H,

    /// <summary>I キー。</summary>
    I,

    /// <summary>J キー。</summary>
    J,

    /// <summary>K キー。</summary>
    K,

    /// <summary>L キー。</summary>
    L,

    /// <summary>M キー。</summary>
    M,

    /// <summary>N キー。</summary>
    N,

    /// <summary>O キー。</summary>
    O,

    /// <summary>P キー。</summary>
    P,

    /// <summary>Q キー。</summary>
    Q,

    /// <summary>R キー。</summary>
    R,

    /// <summary>S キー。</summary>
    S,

    /// <summary>T キー。</summary>
    T,

    /// <summary>U キー。</summary>
    U,

    /// <summary>V キー。</summary>
    V,

    /// <summary>W キー。</summary>
    W,

    /// <summary>X キー。</summary>
    X,

    /// <summary>Y キー。</summary>
    Y,

    /// <summary>Z キー。</summary>
    Z,

    /// <summary>LWin キー。</summary>
    LWin,

    /// <summary>RWin キー。</summary>
    RWin,

    /// <summary>Apps キー。</summary>
    Apps,

    /// <summary>Sleep キー。</summary>
    Sleep,

    /// <summary>NumPad0 キー。</summary>
    NumPad0,

    /// <summary>NumPad1 キー。</summary>
    NumPad1,

    /// <summary>NumPad2 キー。</summary>
    NumPad2,

    /// <summary>NumPad3 キー。</summary>
    NumPad3,

    /// <summary>NumPad4 キー。</summary>
    NumPad4,

    /// <summary>NumPad5 キー。</summary>
    NumPad5,

    /// <summary>NumPad6 キー。</summary>
    NumPad6,

    /// <summary>NumPad7 キー。</summary>
    NumPad7,

    /// <summary>NumPad8 キー。</summary>
    NumPad8,

    /// <summary>NumPad9 キー。</summary>
    NumPad9,

    /// <summary>Multiply キー。</summary>
    Multiply,

    /// <summary>Add キー。</summary>
    Add,

    /// <summary>Separator キー。</summary>
    Separator,

    /// <summary>Subtract キー。</summary>
    Subtract,

    /// <summary>Decimal キー。</summary>
    Decimal,

    /// <summary>Divide キー。</summary>
    Divide,

    /// <summary>F1 キー。</summary>
    F1,

    /// <summary>F2 キー。</summary>
    F2,

    /// <summary>F3 キー。</summary>
    F3,

    /// <summary>F4 キー。</summary>
    F4,

    /// <summary>F5 キー。</summary>
    F5,

    /// <summary>F6 キー。</summary>
    F6,

    /// <summary>F7 キー。</summary>
    F7,

    /// <summary>F8 キー。</summary>
    F8,

    /// <summary>F9 キー。</summary>
    F9,

    /// <summary>F10 キー。</summary>
    F10,

    /// <summary>F11 キー。</summary>
    F11,

    /// <summary>F12 キー。</summary>
    F12,

    /// <summary>F13 キー。</summary>
    F13,

    /// <summary>F14 キー。</summary>
    F14,

    /// <summary>F15 キー。</summary>
    F15,

    /// <summary>F16 キー。</summary>
    F16,

    /// <summary>F17 キー。</summary>
    F17,

    /// <summary>F18 キー。</summary>
    F18,

    /// <summary>F19 キー。</summary>
    F19,

    /// <summary>F20 キー。</summary>
    F20,

    /// <summary>F21 キー。</summary>
    F21,

    /// <summary>F22 キー。</summary>
    F22,

    /// <summary>F23 キー。</summary>
    F23,

    /// <summary>F24 キー。</summary>
    F24,

    /// <summary>NumLock キー。</summary>
    NumLock,

    /// <summary>Scroll キー。</summary>
    Scroll,

    /// <summary>LeftShift キー。</summary>
    LeftShift,

    /// <summary>RightShift キー。</summary>
    RightShift,

    /// <summary>LeftCtrl キー。</summary>
    LeftCtrl,

    /// <summary>RightCtrl キー。</summary>
    RightCtrl,

    /// <summary>LeftAlt キー。</summary>
    LeftAlt,

    /// <summary>RightAlt キー。</summary>
    RightAlt,

    /// <summary>BrowserBack キー。</summary>
    BrowserBack,

    /// <summary>BrowserForward キー。</summary>
    BrowserForward,

    /// <summary>BrowserRefresh キー。</summary>
    BrowserRefresh,

    /// <summary>BrowserStop キー。</summary>
    BrowserStop,

    /// <summary>BrowserSearch キー。</summary>
    BrowserSearch,

    /// <summary>BrowserFavorites キー。</summary>
    BrowserFavorites,

    /// <summary>BrowserHome キー。</summary>
    BrowserHome,

    /// <summary>VolumeMute キー。</summary>
    VolumeMute,

    /// <summary>VolumeDown キー。</summary>
    VolumeDown,

    /// <summary>VolumeUp キー。</summary>
    VolumeUp,

    /// <summary>MediaNextTrack キー。</summary>
    MediaNextTrack,

    /// <summary>MediaPreviousTrack キー。</summary>
    MediaPreviousTrack,

    /// <summary>MediaStop キー。</summary>
    MediaStop,

    /// <summary>MediaPlayPause キー。</summary>
    MediaPlayPause,

    /// <summary>LaunchMail キー。</summary>
    LaunchMail,

    /// <summary>SelectMedia キー。</summary>
    SelectMedia,

    /// <summary>LaunchApplication1 キー。</summary>
    LaunchApplication1,

    /// <summary>LaunchApplication2 キー。</summary>
    LaunchApplication2,

    /// <summary>OemSemicolon キー。</summary>
    OemSemicolon,

    /// <summary>Oem1 キー。</summary>
    Oem1,

    /// <summary>OemPlus キー。</summary>
    OemPlus,

    /// <summary>OemComma キー。</summary>
    OemComma,

    /// <summary>OemMinus キー。</summary>
    OemMinus,

    /// <summary>OemPeriod キー。</summary>
    OemPeriod,

    /// <summary>OemQuestion キー。</summary>
    OemQuestion,

    /// <summary>Oem2 キー。</summary>
    Oem2,

    /// <summary>OemTilde キー。</summary>
    OemTilde,

    /// <summary>Oem3 キー。</summary>
    Oem3,

    /// <summary>AbntC1 キー。</summary>
    AbntC1,

    /// <summary>AbntC2 キー。</summary>
    AbntC2,

    /// <summary>OemOpenBrackets キー。</summary>
    OemOpenBrackets,

    /// <summary>Oem4 キー。</summary>
    Oem4,

    /// <summary>OemPipe キー。</summary>
    OemPipe,

    /// <summary>Oem5 キー。</summary>
    Oem5,

    /// <summary>OemCloseBrackets キー。</summary>
    OemCloseBrackets,

    /// <summary>Oem6 キー。</summary>
    Oem6,

    /// <summary>OemQuotes キー。</summary>
    OemQuotes,

    /// <summary>Oem7 キー。</summary>
    Oem7,

    /// <summary>Oem8 キー。</summary>
    Oem8,

    /// <summary>OemBackslash キー。</summary>
    OemBackslash,

    /// <summary>Oem102 キー。</summary>
    Oem102,

    /// <summary>ImeProcessed キー。</summary>
    ImeProcessed,

    /// <summary>System キー。</summary>
    System,

    /// <summary>OemAttn キー。</summary>
    OemAttn,

    /// <summary>DbeAlphanumeric キー。</summary>
    DbeAlphanumeric,

    /// <summary>OemFinish キー。</summary>
    OemFinish,

    /// <summary>DbeKatakana キー。</summary>
    DbeKatakana,

    /// <summary>DbeHiragana キー。</summary>
    DbeHiragana,

    /// <summary>OemCopy キー。</summary>
    OemCopy,

    /// <summary>DbeSbcsChar キー。</summary>
    DbeSbcsChar,

    /// <summary>OemAuto キー。</summary>
    OemAuto,

    /// <summary>DbeDbcsChar キー。</summary>
    DbeDbcsChar,

    /// <summary>OemEnlw キー。</summary>
    OemEnlw,

    /// <summary>OemBackTab キー。</summary>
    OemBackTab,

    /// <summary>DbeRoman キー。</summary>
    DbeRoman,

    /// <summary>DbeNoRoman キー。</summary>
    DbeNoRoman,

    /// <summary>Attn キー。</summary>
    Attn,

    /// <summary>CrSel キー。</summary>
    CrSel,

    /// <summary>DbeEnterWordRegisterMode キー。</summary>
    DbeEnterWordRegisterMode,

    /// <summary>ExSel キー。</summary>
    ExSel,

    /// <summary>DbeEnterImeConfigureMode キー。</summary>
    DbeEnterImeConfigureMode,

    /// <summary>EraseEof キー。</summary>
    EraseEof,

    /// <summary>DbeFlushString キー。</summary>
    DbeFlushString,

    /// <summary>Play キー。</summary>
    Play,

    /// <summary>DbeCodeInput キー。</summary>
    DbeCodeInput,

    /// <summary>DbeNoCodeInput キー。</summary>
    DbeNoCodeInput,

    /// <summary>Zoom キー。</summary>
    Zoom,

    /// <summary>NoName キー。</summary>
    NoName,

    /// <summary>DbeDetermineString キー。</summary>
    DbeDetermineString,

    /// <summary>DbeEnterDialogConversionMode キー。</summary>
    DbeEnterDialogConversionMode,

    /// <summary>Pa1 キー。</summary>
    Pa1,

    /// <summary>OemClear キー。</summary>
    OemClear,

    /// <summary>DeadCharProcessed キー。</summary>
    DeadCharProcessed,

    /// <summary>FnLeftArrow キー。</summary>
    FnLeftArrow,

    /// <summary>FnRightArrow キー。</summary>
    FnRightArrow,

    /// <summary>FnUpArrow キー。</summary>
    FnUpArrow,

    /// <summary>FnDownArrow キー。</summary>
    FnDownArrow,

    /// <summary>MediaHome キー。</summary>
    MediaHome,

    /// <summary>MediaChannelList キー。</summary>
    MediaChannelList,

    /// <summary>MediaChannelRaise キー。</summary>
    MediaChannelRaise,

    /// <summary>MediaChannelLower キー。</summary>
    MediaChannelLower,

    /// <summary>MediaRecord キー。</summary>
    MediaRecord,

    /// <summary>MediaRed キー。</summary>
    MediaRed,

    /// <summary>MediaGreen キー。</summary>
    MediaGreen,

    /// <summary>MediaYellow キー。</summary>
    MediaYellow,

    /// <summary>MediaBlue キー。</summary>
    MediaBlue,

    /// <summary>MediaMenu キー。</summary>
    MediaMenu,

    /// <summary>MediaMore キー。</summary>
    MediaMore,

    /// <summary>MediaOption キー。</summary>
    MediaOption,

    /// <summary>MediaInfo キー。</summary>
    MediaInfo,

    /// <summary>MediaSearch キー。</summary>
    MediaSearch,

    /// <summary>MediaSubtitle キー。</summary>
    MediaSubtitle,

    /// <summary>MediaTvGuide キー。</summary>
    MediaTvGuide,

    /// <summary>MediaPreviousChannel キー。</summary>
    MediaPreviousChannel,

}

