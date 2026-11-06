using ClipboardApp.Core.Services;
using Xunit;

namespace ClipboardApp.Tests
{
    /// <summary>
    /// The hotkey shown in the title bar and captured in Settings must round-trip
    /// through the exact same parser that registers the Win32 hotkey, otherwise a
    /// captured combination could silently fail to register.
    /// </summary>
    public class HotkeyParserTests
    {
        [Theory]
        [InlineData("Ctrl+`", HotkeyParser.ModControl, 0xC0)]
        [InlineData("Ctrl+F1", HotkeyParser.ModControl, 0x70)]
        [InlineData("Control+Tilde", HotkeyParser.ModControl, 0xC0)]
        [InlineData("Ctrl+Shift+F12", HotkeyParser.ModControl | HotkeyParser.ModShift, 0x70 + 11)]
        [InlineData("Alt+Space", HotkeyParser.ModAlt, 0x20)]
        [InlineData("Win+A", HotkeyParser.ModWin, 'A')]
        public void ParsesSupportedCombinations(string text, int expectedModifiers, int expectedKey)
        {
            Assert.True(HotkeyParser.TryParse(text, out int mods, out int vk));
            Assert.Equal(expectedModifiers, mods);
            Assert.Equal(expectedKey, vk);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Ctrl")]
        [InlineData("Ctrl+Nonsense")]
        [InlineData("Ctrl+F25")]
        public void RejectsEmptyOrKeylessCombinations(string text)
        {
            Assert.False(HotkeyParser.TryParse(text, out _, out _));
        }

        [Fact]
        public void FormatIsCanonicalAndRoundTrips()
        {
            int mods = HotkeyParser.ModControl | HotkeyParser.ModWin;
            string formatted = HotkeyParser.Format(mods, 0x70);
            Assert.Equal("Ctrl+Win+F1", formatted);

            Assert.True(HotkeyParser.TryParse(formatted, out int reMods, out int reVk));
            Assert.Equal(mods, reMods);
            Assert.Equal(0x70, reVk);
        }

        [Fact]
        public void DisplayStringNormalizesAliases()
        {
            Assert.Equal("Ctrl+F1", HotkeyParser.ToDisplayString("Control+f1"));
            Assert.Equal("Ctrl+`", HotkeyParser.ToDisplayString("Ctrl+Tilde"));
        }

        [Theory]
        [InlineData(HotkeyParser.ModControl, true)]
        [InlineData(HotkeyParser.ModWin, true)]
        [InlineData(0, false)]
        public void ModifierRequirementIsEnforced(int modifiers, bool expected)
        {
            Assert.Equal(expected, HotkeyParser.HasModifier(modifiers));
        }

        [Fact]
        public void FunctionKeyBoundaries()
        {
            Assert.True(HotkeyParser.TryParseFunctionKey("F1", out int f1));
            Assert.Equal(HotkeyParser.VkF1, f1);

            Assert.True(HotkeyParser.TryParseFunctionKey("F24", out int f24));
            Assert.Equal(HotkeyParser.VkF24, f24);

            Assert.False(HotkeyParser.TryParseFunctionKey("F0", out _));
            Assert.False(HotkeyParser.TryParseFunctionKey("F25", out _));
        }
    }
}
