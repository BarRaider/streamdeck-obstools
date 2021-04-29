using BarRaider.SdTools;
using OTI.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.ObsTools.Wrappers
{
    internal class HotkeySequence
    {
        public bool IsValidSequence { get; private set; }
        public bool CtrlPressed { get; private set; } = false;
        public bool AltPressed { get; private set; } = false;
        public bool ShiftPressed { get; private set; } = false;
        public bool WinPressed { get; private set; } = false;
        public VirtualKeyCode Keycode { get; private set; }

        public HotkeySequence(string sequence)
        {
            IsValidSequence = false;
            ParseSequence(sequence);
        }

        private void ParseSequence(string sequence)
        {
            try
            {
                if (string.IsNullOrEmpty(sequence))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} received empty/null sequence");
                    return;
                }

                string[] modifiers = sequence.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                if (modifiers.Length == 0)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} invalid sequence: {sequence}");
                    return;
                }

                for (int idx = 0; idx < modifiers.Length; idx++)
                {
                    string currModifier = modifiers[idx].Trim().ToUpperInvariant();
                    if (currModifier == "CTRL")
                    {
                        CtrlPressed = true;
                    }
                    else if (currModifier == "ALT")
                    {
                        AltPressed = true;
                    }
                    else if (currModifier == "SHIFT")
                    {
                        ShiftPressed = true;
                    }
                    else if (currModifier == "WIN")
                    {
                        WinPressed = true;
                    }
                    else if (idx < modifiers.Length - 1) // Not final entry, it should have been one of the above!
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} invalid sequence: {sequence}. Expected Key Modifier, got {currModifier}");
                        return;
                    }
                    else // Final entry
                    {
                        if (currModifier.Length == 1)
                        {
                            Keycode = (VirtualKeyCode)currModifier[0];
                        }
                        else
                        {
                            string text = ConvertSimilarMacroCommands(currModifier);
                            Keycode = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), text, true);
                        }
                    }
                }

                IsValidSequence = true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} exception when parsing sequence: {sequence} Exception: {ex}");
            }
        }

        private string ConvertSimilarMacroCommands(string macroText)
        {
            switch (macroText)
            {
                case "CTRL":
                    return "CONTROL";
                case "LCTRL":
                    return "LCONTROL";
                case "RCTRL":
                    return "RCONTROL";
                case "ALT":
                    return "MENU";
                case "LALT":
                    return "LMENU";
                case "RALT":
                    return "RMENU";
                case "ENTER":
                    return "RETURN";
                case "BACKSPACE":
                    return "BACK";
                case "WIN":
                    return "LWIN";
                case "WINDOWS":
                    return "LWIN";
                case "PAGEUP":
                case "PGUP":
                    return "PRIOR";
                case "PAGEDOWN":
                case "PGDN":
                    return "NEXT";
                case "BREAK":
                    return "PAUSE";
                case "ARROWDOWN":
                    return "DOWN";
                case "ARROWUP":
                    return "UP";
                case "ARROWRIGHT":
                    return "RIGHT";
                case "ARROWLEFT":
                    return "LEFT";
            };

            return macroText;
        }

    }
}
