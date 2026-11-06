using System;
using System.Collections.Generic;
using System.Text;

namespace ClipboardApp.Core.Services
{
    /// <summary>
    /// Converts between the persisted hotkey string (for example "Ctrl+Shift+F1")
    /// and Win32 modifier / virtual-key values, and produces a human-readable
    /// form for the title bar and settings UI.
    ///
    /// The parser is deliberately permissive: it accepts "Control", "Win",
    /// "Tilde", "F24", single characters and function keys, so settings written
    /// by older releases keep loading.
    /// </summary>
    public static class HotkeyParser
    {
        public const int ModAlt = 0x0001;
        public const int ModControl = 0x0002;
        public const int ModShift = 0x0004;
        public const int ModWin = 0x0008;
        public const int ModNoRepeat = 0x4000;

        public const int VkTilde = 0xC0;
        public const int VkF1 = 0x70;
        public const int VkF24 = 0x87;

        /// <summary>
        /// Parses a hotkey string into Win32 modifier flags (without MOD_NOREPEAT)
        /// and a virtual-key code. Returns false when no key could be resolved.
        /// </summary>
        public static bool TryParse(string? hotkey, out int modifiers, out int virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(hotkey)) return false;

            foreach (var rawPart in hotkey.Split('+'))
            {
                var part = rawPart.Trim();
                if (part.Length == 0) continue;

                switch (part.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= ModControl;
                        continue;
                    case "ALT":
                        modifiers |= ModAlt;
                        continue;
                    case "SHIFT":
                        modifiers |= ModShift;
                        continue;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= ModWin;
                        continue;
                    case "TILDE":
                    case "`":
                    case "~":
                        virtualKey = VkTilde;
                        continue;
                    case "SPACE":
                        virtualKey = 0x20;
                        continue;
                    case "TAB":
                        virtualKey = 0x09;
                        continue;
                    case "ESC":
                    case "ESCAPE":
                        virtualKey = 0x1B;
                        continue;
                }

                if (part.Length == 1)
                {
                    virtualKey = char.ToUpperInvariant(part[0]);
                    continue;
                }

                if (TryParseFunctionKey(part, out int functionKey))
                {
                    virtualKey = functionKey;
                }
            }

            return virtualKey != 0;
        }

        public static bool TryParseFunctionKey(string part, out int virtualKey)
        {
            virtualKey = 0;
            if (part.Length < 2 || part.Length > 3) return false;
            if (part[0] != 'F' && part[0] != 'f') return false;
            if (!int.TryParse(part.AsSpan(1), out int number)) return false;
            if (number < 1 || number > 24) return false;
            virtualKey = VkF1 + (number - 1);
            return true;
        }

        /// <summary>
        /// Builds the canonical persisted form, always in the order
        /// Ctrl+Alt+Shift+Win+Key so the same combination always serializes
        /// to the same string.
        /// </summary>
        public static string Format(int modifiers, int virtualKey)
        {
            var sb = new StringBuilder();
            if ((modifiers & ModControl) != 0) sb.Append("Ctrl+");
            if ((modifiers & ModShift) != 0) sb.Append("Shift+");
            if ((modifiers & ModAlt) != 0) sb.Append("Alt+");
            if ((modifiers & ModWin) != 0) sb.Append("Win+");
            sb.Append(FormatKey(virtualKey));
            return sb.ToString();
        }

        /// <summary>
        /// Human-readable display form used by the title bar and the settings
        /// row, e.g. "Ctrl+F1" or "Ctrl+Win+`".
        /// </summary>
        public static string ToDisplayString(string? hotkey)
        {
            if (!TryParse(hotkey, out int modifiers, out int virtualKey)) return hotkey ?? "";
            return Format(modifiers, virtualKey);
        }

        public static string FormatKey(int virtualKey)
        {
            if (virtualKey >= VkF1 && virtualKey <= VkF24) return "F" + (virtualKey - VkF1 + 1);
            switch (virtualKey)
            {
                case VkTilde: return "`";
                case 0x20: return "Space";
                case 0x09: return "Tab";
                case 0x1B: return "Esc";
                case 0x24: return "Home";
                case 0x23: return "End";
                case 0x21: return "PageUp";
                case 0x22: return "PageDown";
                case 0x2D: return "Insert";
                case 0x2E: return "Delete";
                case 0x08: return "Backspace";
                case 0x0D: return "Enter";
                case 0x25: return "Left";
                case 0x26: return "Up";
                case 0x27: return "Right";
                case 0x28: return "Down";
                case 0xBA: return ";";
                case 0xBB: return "=";
                case 0xBC: return ",";
                case 0xBD: return "-";
                case 0xBE: return ".";
                case 0xBF: return "/";
                case 0xDB: return "[";
                case 0xDC: return "\\";
                case 0xDD: return "]";
                case 0xDE: return "'";
            }

            if (virtualKey >= 0x30 && virtualKey <= 0x39) return ((char)virtualKey).ToString();
            if (virtualKey >= 0x41 && virtualKey <= 0x5A) return ((char)virtualKey).ToString();
            return "VK" + virtualKey.ToString("X2");
        }

        /// <summary>
        /// A hotkey without Ctrl, Alt, Shift or Win would swallow a bare
        /// keystroke system-wide, so it is rejected in the capture UI.
        /// </summary>
        public static bool HasModifier(int modifiers)
            => (modifiers & (ModControl | ModAlt | ModShift | ModWin)) != 0;

        /// <summary>
        /// Sensible default used when a captured combination is invalid or
        /// already taken by another application.
        /// </summary>
        public const string DefaultHotkey = "Ctrl+`";

        public static IReadOnlyList<string> DescribeModifiers(int modifiers)
        {
            var list = new List<string>();
            if ((modifiers & ModControl) != 0) list.Add("Ctrl");
            if ((modifiers & ModShift) != 0) list.Add("Shift");
            if ((modifiers & ModAlt) != 0) list.Add("Alt");
            if ((modifiers & ModWin) != 0) list.Add("Win");
            return list;
        }
    }
}
