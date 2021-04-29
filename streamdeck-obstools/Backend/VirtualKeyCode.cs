namespace WindowsInput.Native
{
    //
    // Summary:
    //     The list of VirtualKeyCodes (see: http://msdn.microsoft.com/en-us/library/ms645540(VS.85).aspx)
    public enum VirtualKeyCode
    {
        //
        // Summary:
        //     Left mouse button
        LBUTTON = 1,
        //
        // Summary:
        //     Right mouse button
        RBUTTON = 2,
        //
        // Summary:
        //     Control-break processing
        CANCEL = 3,
        //
        // Summary:
        //     Middle mouse button (three-button mouse) - NOT contiguous with LBUTTON and RBUTTON
        MBUTTON = 4,
        //
        // Summary:
        //     Windows 2000/XP: X1 mouse button - NOT contiguous with LBUTTON and RBUTTON
        XBUTTON1 = 5,
        //
        // Summary:
        //     Windows 2000/XP: X2 mouse button - NOT contiguous with LBUTTON and RBUTTON
        XBUTTON2 = 6,
        //
        // Summary:
        //     BACKSPACE key
        BACK = 8,
        //
        // Summary:
        //     TAB key
        TAB = 9,
        //
        // Summary:
        //     CLEAR key
        CLEAR = 12,
        //
        // Summary:
        //     ENTER key
        RETURN = 13,
        //
        // Summary:
        //     SHIFT key
        SHIFT = 16,
        //
        // Summary:
        //     CTRL key
        CONTROL = 17,
        //
        // Summary:
        //     ALT key
        MENU = 18,
        //
        // Summary:
        //     PAUSE key
        PAUSE = 19,
        //
        // Summary:
        //     CAPS LOCK key
        CAPITAL = 20,
        //
        // Summary:
        //     Input Method Editor (IME) Kana mode
        KANA = 21,
        //
        // Summary:
        //     IME Hanguel mode (maintained for compatibility; use HANGUL)
        HANGEUL = 21,
        //
        // Summary:
        //     IME Hangul mode
        HANGUL = 21,
        //
        // Summary:
        //     IME Junja mode
        JUNJA = 23,
        //
        // Summary:
        //     IME final mode
        FINAL = 24,
        //
        // Summary:
        //     IME Hanja mode
        HANJA = 25,
        //
        // Summary:
        //     IME Kanji mode
        KANJI = 25,
        //
        // Summary:
        //     ESC key
        ESCAPE = 27,
        //
        // Summary:
        //     IME convert
        CONVERT = 28,
        //
        // Summary:
        //     IME nonconvert
        NONCONVERT = 29,
        //
        // Summary:
        //     IME accept
        ACCEPT = 30,
        //
        // Summary:
        //     IME mode change request
        MODECHANGE = 31,
        //
        // Summary:
        //     SPACEBAR
        SPACE = 32,
        //
        // Summary:
        //     PAGE UP key
        PRIOR = 33,
        //
        // Summary:
        //     PAGE DOWN key
        NEXT = 34,
        //
        // Summary:
        //     END key
        END = 35,
        //
        // Summary:
        //     HOME key
        HOME = 36,
        //
        // Summary:
        //     LEFT ARROW key
        LEFT = 37,
        //
        // Summary:
        //     UP ARROW key
        UP = 38,
        //
        // Summary:
        //     RIGHT ARROW key
        RIGHT = 39,
        //
        // Summary:
        //     DOWN ARROW key
        DOWN = 40,
        //
        // Summary:
        //     SELECT key
        SELECT = 41,
        //
        // Summary:
        //     PRINT key
        PRINT = 42,
        //
        // Summary:
        //     EXECUTE key
        EXECUTE = 43,
        //
        // Summary:
        //     PRINT SCREEN key
        SNAPSHOT = 44,
        //
        // Summary:
        //     INS key
        INSERT = 45,
        //
        // Summary:
        //     DEL key
        DELETE = 46,
        //
        // Summary:
        //     HELP key
        HELP = 47,
        //
        // Summary:
        //     0 key
        VK_0 = 48,
        //
        // Summary:
        //     1 key
        VK_1 = 49,
        //
        // Summary:
        //     2 key
        VK_2 = 50,
        //
        // Summary:
        //     3 key
        VK_3 = 51,
        //
        // Summary:
        //     4 key
        VK_4 = 52,
        //
        // Summary:
        //     5 key
        VK_5 = 53,
        //
        // Summary:
        //     6 key
        VK_6 = 54,
        //
        // Summary:
        //     7 key
        VK_7 = 55,
        //
        // Summary:
        //     8 key
        VK_8 = 56,
        //
        // Summary:
        //     9 key
        VK_9 = 57,
        //
        // Summary:
        //     A key
        VK_A = 65,
        //
        // Summary:
        //     B key
        VK_B = 66,
        //
        // Summary:
        //     C key
        VK_C = 67,
        //
        // Summary:
        //     D key
        VK_D = 68,
        //
        // Summary:
        //     E key
        VK_E = 69,
        //
        // Summary:
        //     F key
        VK_F = 70,
        //
        // Summary:
        //     G key
        VK_G = 71,
        //
        // Summary:
        //     H key
        VK_H = 72,
        //
        // Summary:
        //     I key
        VK_I = 73,
        //
        // Summary:
        //     J key
        VK_J = 74,
        //
        // Summary:
        //     K key
        VK_K = 75,
        //
        // Summary:
        //     L key
        VK_L = 76,
        //
        // Summary:
        //     M key
        VK_M = 77,
        //
        // Summary:
        //     N key
        VK_N = 78,
        //
        // Summary:
        //     O key
        VK_O = 79,
        //
        // Summary:
        //     P key
        VK_P = 80,
        //
        // Summary:
        //     Q key
        VK_Q = 81,
        //
        // Summary:
        //     R key
        VK_R = 82,
        //
        // Summary:
        //     S key
        VK_S = 83,
        //
        // Summary:
        //     T key
        VK_T = 84,
        //
        // Summary:
        //     U key
        VK_U = 85,
        //
        // Summary:
        //     V key
        VK_V = 86,
        //
        // Summary:
        //     W key
        VK_W = 87,
        //
        // Summary:
        //     X key
        VK_X = 88,
        //
        // Summary:
        //     Y key
        VK_Y = 89,
        //
        // Summary:
        //     Z key
        VK_Z = 90,
        //
        // Summary:
        //     Left Windows key (Microsoft Natural keyboard)
        LWIN = 91,
        //
        // Summary:
        //     Right Windows key (Natural keyboard)
        RWIN = 92,
        //
        // Summary:
        //     Applications key (Natural keyboard)
        APPS = 93,
        //
        // Summary:
        //     Computer Sleep key
        SLEEP = 95,
        //
        // Summary:
        //     Numeric keypad 0 key
        NUMPAD0 = 96,
        //
        // Summary:
        //     Numeric keypad 1 key
        NUMPAD1 = 97,
        //
        // Summary:
        //     Numeric keypad 2 key
        NUMPAD2 = 98,
        //
        // Summary:
        //     Numeric keypad 3 key
        NUMPAD3 = 99,
        //
        // Summary:
        //     Numeric keypad 4 key
        NUMPAD4 = 100,
        //
        // Summary:
        //     Numeric keypad 5 key
        NUMPAD5 = 101,
        //
        // Summary:
        //     Numeric keypad 6 key
        NUMPAD6 = 102,
        //
        // Summary:
        //     Numeric keypad 7 key
        NUMPAD7 = 103,
        //
        // Summary:
        //     Numeric keypad 8 key
        NUMPAD8 = 104,
        //
        // Summary:
        //     Numeric keypad 9 key
        NUMPAD9 = 105,
        //
        // Summary:
        //     Multiply key
        MULTIPLY = 106,
        //
        // Summary:
        //     Add key
        ADD = 107,
        //
        // Summary:
        //     Separator key
        SEPARATOR = 108,
        //
        // Summary:
        //     Subtract key
        SUBTRACT = 109,
        //
        // Summary:
        //     Decimal key
        DECIMAL = 110,
        //
        // Summary:
        //     Divide key
        DIVIDE = 111,
        //
        // Summary:
        //     F1 key
        F1 = 112,
        //
        // Summary:
        //     F2 key
        F2 = 113,
        //
        // Summary:
        //     F3 key
        F3 = 114,
        //
        // Summary:
        //     F4 key
        F4 = 115,
        //
        // Summary:
        //     F5 key
        F5 = 116,
        //
        // Summary:
        //     F6 key
        F6 = 117,
        //
        // Summary:
        //     F7 key
        F7 = 118,
        //
        // Summary:
        //     F8 key
        F8 = 119,
        //
        // Summary:
        //     F9 key
        F9 = 120,
        //
        // Summary:
        //     F10 key
        F10 = 121,
        //
        // Summary:
        //     F11 key
        F11 = 122,
        //
        // Summary:
        //     F12 key
        F12 = 123,
        //
        // Summary:
        //     F13 key
        F13 = 124,
        //
        // Summary:
        //     F14 key
        F14 = 125,
        //
        // Summary:
        //     F15 key
        F15 = 126,
        //
        // Summary:
        //     F16 key
        F16 = 127,
        //
        // Summary:
        //     F17 key
        F17 = 128,
        //
        // Summary:
        //     F18 key
        F18 = 129,
        //
        // Summary:
        //     F19 key
        F19 = 130,
        //
        // Summary:
        //     F20 key
        F20 = 131,
        //
        // Summary:
        //     F21 key
        F21 = 132,
        //
        // Summary:
        //     F22 key
        F22 = 133,
        //
        // Summary:
        //     F23 key
        F23 = 134,
        //
        // Summary:
        //     F24 key
        F24 = 135,
        //
        // Summary:
        //     NUM LOCK key
        NUMLOCK = 144,
        //
        // Summary:
        //     SCROLL LOCK key
        SCROLL = 145,
        //
        // Summary:
        //     Left SHIFT key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        LSHIFT = 160,
        //
        // Summary:
        //     Right SHIFT key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        RSHIFT = 161,
        //
        // Summary:
        //     Left CONTROL key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        LCONTROL = 162,
        //
        // Summary:
        //     Right CONTROL key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        RCONTROL = 163,
        //
        // Summary:
        //     Left MENU key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        LMENU = 164,
        //
        // Summary:
        //     Right MENU key - Used only as parameters to GetAsyncKeyState() and GetKeyState()
        RMENU = 165,
        //
        // Summary:
        //     Windows 2000/XP: Browser Back key
        BROWSER_BACK = 166,
        //
        // Summary:
        //     Windows 2000/XP: Browser Forward key
        BROWSER_FORWARD = 167,
        //
        // Summary:
        //     Windows 2000/XP: Browser Refresh key
        BROWSER_REFRESH = 168,
        //
        // Summary:
        //     Windows 2000/XP: Browser Stop key
        BROWSER_STOP = 169,
        //
        // Summary:
        //     Windows 2000/XP: Browser Search key
        BROWSER_SEARCH = 170,
        //
        // Summary:
        //     Windows 2000/XP: Browser Favorites key
        BROWSER_FAVORITES = 171,
        //
        // Summary:
        //     Windows 2000/XP: Browser Start and Home key
        BROWSER_HOME = 172,
        //
        // Summary:
        //     Windows 2000/XP: Volume Mute key
        VOLUME_MUTE = 173,
        //
        // Summary:
        //     Windows 2000/XP: Volume Down key
        VOLUME_DOWN = 174,
        //
        // Summary:
        //     Windows 2000/XP: Volume Up key
        VOLUME_UP = 175,
        //
        // Summary:
        //     Windows 2000/XP: Next Track key
        MEDIA_NEXT_TRACK = 176,
        //
        // Summary:
        //     Windows 2000/XP: Previous Track key
        MEDIA_PREV_TRACK = 177,
        //
        // Summary:
        //     Windows 2000/XP: Stop Media key
        MEDIA_STOP = 178,
        //
        // Summary:
        //     Windows 2000/XP: Play/Pause Media key
        MEDIA_PLAY_PAUSE = 179,
        //
        // Summary:
        //     Windows 2000/XP: Start Mail key
        LAUNCH_MAIL = 180,
        //
        // Summary:
        //     Windows 2000/XP: Select Media key
        LAUNCH_MEDIA_SELECT = 181,
        //
        // Summary:
        //     Windows 2000/XP: Start Application 1 key
        LAUNCH_APP1 = 182,
        //
        // Summary:
        //     Windows 2000/XP: Start Application 2 key
        LAUNCH_APP2 = 183,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the ';:' key
        OEM_1 = 186,
        //
        // Summary:
        //     Windows 2000/XP: For any country/region, the '+' key
        OEM_PLUS = 187,
        //
        // Summary:
        //     Windows 2000/XP: For any country/region, the ',' key
        OEM_COMMA = 188,
        //
        // Summary:
        //     Windows 2000/XP: For any country/region, the '-' key
        OEM_MINUS = 189,
        //
        // Summary:
        //     Windows 2000/XP: For any country/region, the '.' key
        OEM_PERIOD = 190,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the '/?' key
        OEM_2 = 191,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the '`~' key
        OEM_3 = 192,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the '[{' key
        OEM_4 = 219,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the '\|' key
        OEM_5 = 220,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the ']}' key
        OEM_6 = 221,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard. Windows 2000/XP:
        //     For the US standard keyboard, the 'single-quote/double-quote' key
        OEM_7 = 222,
        //
        // Summary:
        //     Used for miscellaneous characters; it can vary by keyboard.
        OEM_8 = 223,
        //
        // Summary:
        //     Windows 2000/XP: Either the angle bracket key or the backslash key on the RT
        //     102-key keyboard
        OEM_102 = 226,
        //
        // Summary:
        //     Windows 95/98/Me, Windows NT 4.0, Windows 2000/XP: IME PROCESS key
        PROCESSKEY = 229,
        //
        // Summary:
        //     Windows 2000/XP: Used to pass Unicode characters as if they were keystrokes.
        //     The PACKET key is the low word of a 32-bit Virtual Key value used for non-keyboard
        //     input methods. For more information, see Remark in KEYBDINPUT, SendInput, WM_KEYDOWN,
        //     and WM_KEYUP
        PACKET = 231,
        //
        // Summary:
        //     Attn key
        ATTN = 246,
        //
        // Summary:
        //     CrSel key
        CRSEL = 247,
        //
        // Summary:
        //     ExSel key
        EXSEL = 248,
        //
        // Summary:
        //     Erase EOF key
        EREOF = 249,
        //
        // Summary:
        //     Play key
        PLAY = 250,
        //
        // Summary:
        //     Zoom key
        ZOOM = 251,
        //
        // Summary:
        //     Reserved
        NONAME = 252,
        //
        // Summary:
        //     PA1 key
        PA1 = 253,
        //
        // Summary:
        //     Clear key
        OEM_CLEAR = 254,
        //
        // Summary:
        //     Super Macro Extended Numpad Up
        NUMPAD_UP = 256,
        //
        // Summary:
        //     Super Macro Extended Numpad Down
        NUMPAD_DOWN = 257,
        //
        // Summary:
        //     Super Macro Extended Numpad Left
        NUMPAD_LEFT = 258,
        //
        // Summary:
        //     Super Macro Extended Numpad Right
        NUMPAD_RIGHT = 259,
        //
        // Summary:
        //     Super Macro Extended Numpad Home
        NUMPAD_HOME = 260,
        //
        // Summary:
        //     Super Macro Extended Numpad End
        NUMPAD_END = 261,
        //
        // Summary:
        //     Super Macro Extended Numpad Insert
        NUMPAD_INSERT = 262,
        //
        // Summary:
        //     Super Macro Extended Numpad Del
        NUMPAD_DEL = 263,
        //
        // Summary:
        //     Super Macro Extended Numpad Page up
        NUMPAD_PAGEUP = 264,
        //
        // Summary:
        //     Super Macro Extended Numpad Page down
        NUMPAD_PAGEDOWN = 265
    }
}