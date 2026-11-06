using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.UI
{
    public sealed class UiStyleTokens
    {
        public bool IsDark { get; init; }
        public Color WindowBg { get; init; }
        public Color CardBg { get; init; }
        public Color InputBg { get; init; }
        public Color HoverBg { get; init; }
        public Color Border { get; init; }
        public Color TextPrimary { get; init; }
        public Color TextSecondary { get; init; }
        public Color Accent { get; init; }
        public Color AccentText { get; init; }
    }

    /// <summary>
    /// Applies one consistent, switchable visual style to every app-owned
    /// dialog, inline form and menu. The master toggle can restore native
    /// Windows colors without rebuilding the UI.
    /// </summary>
    public static class UiStyler
    {
        public static AppSettings? ActiveSettings { get; set; }

        public static AppSettings CurrentSettings => ActiveSettings ?? new AppSettings();

        public static bool IsDark(AppSettings settings) =>
            settings.Theme == "Dark" ||
            (settings.Theme == "System" && AppSettings.IsWindowsInDarkMode());

        public static bool UseCustomStyling(AppSettings settings) =>
            settings.EnableCustomUiStyling &&
            !string.Equals(settings.VisualStyle, "Native", StringComparison.OrdinalIgnoreCase);

        public static UiStyleTokens GetTokens(AppSettings? settings = null)
        {
            settings ??= CurrentSettings;
            bool dark = IsDark(settings);

            if (string.Equals(settings.VisualStyle, "Classic", StringComparison.OrdinalIgnoreCase))
            {
                return new UiStyleTokens
                {
                    IsDark = dark,
                    WindowBg = dark ? Color.FromArgb(28, 28, 28) : Color.FromArgb(240, 240, 240),
                    CardBg = dark ? Color.FromArgb(42, 42, 42) : SystemColors.Window,
                    InputBg = dark ? Color.FromArgb(36, 36, 36) : SystemColors.Window,
                    HoverBg = dark ? Color.FromArgb(54, 54, 54) : Color.FromArgb(228, 228, 228),
                    Border = dark ? Color.FromArgb(72, 72, 72) : SystemColors.ControlDark,
                    TextPrimary = dark ? Color.FromArgb(238, 238, 238) : SystemColors.ControlText,
                    TextSecondary = dark ? Color.FromArgb(172, 172, 172) : SystemColors.GrayText,
                    Accent = SystemColors.Highlight,
                    AccentText = SystemColors.HighlightText
                };
            }

            if (string.Equals(settings.VisualStyle, "Acrylic", StringComparison.OrdinalIgnoreCase))
            {
                return new UiStyleTokens
                {
                    IsDark = dark,
                    WindowBg = dark ? Color.FromArgb(20, 20, 24) : Color.FromArgb(246, 249, 252),
                    CardBg = dark ? ModernPalette.AcrylicCardBgDark : ModernPalette.AcrylicCardBgLight,
                    InputBg = dark ? Color.FromArgb(45, 45, 52) : Color.FromArgb(255, 255, 255),
                    HoverBg = dark ? Color.FromArgb(62, 62, 72) : Color.FromArgb(235, 242, 248),
                    Border = dark ? ModernPalette.AcrylicCardBorderDark : ModernPalette.AcrylicCardBorderLight,
                    TextPrimary = dark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary,
                    TextSecondary = dark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                    Accent = dark ? ModernPalette.DarkAccent : ModernPalette.LightAccent,
                    AccentText = Color.White
                };
            }

            return new UiStyleTokens
            {
                IsDark = dark,
                WindowBg = dark ? ModernPalette.DarkWindowBg : ModernPalette.LightWindowBg,
                CardBg = dark ? ModernPalette.DarkCardBg : ModernPalette.LightCardBg,
                InputBg = dark ? ModernPalette.DarkSearchBg : ModernPalette.LightSearchBg,
                HoverBg = dark ? ModernPalette.DarkCardHover : ModernPalette.LightCardHover,
                Border = dark ? ModernPalette.DarkCardBorder : ModernPalette.LightCardBorder,
                TextPrimary = dark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary,
                TextSecondary = dark ? ModernPalette.DarkTextSecondary : ModernPalette.LightTextSecondary,
                Accent = dark ? ModernPalette.DarkAccent : ModernPalette.LightAccent,
                AccentText = Color.White
            };
        }

        public static void ApplyForm(Form form, AppSettings? settings = null)
        {
            if (form == null) return;
            settings ??= CurrentSettings;

            if (!UseCustomStyling(settings))
            {
                ApplyNativeForm(form);
                return;
            }

            var tokens = GetTokens(settings);
            form.SuspendLayout();
            try
            {
                form.BackColor = tokens.WindowBg;
                form.ForeColor = tokens.TextPrimary;
                if (form.Font == null || form.Font.FontFamily.Name == "Microsoft Sans Serif")
                {
                    form.Font = new Font("Segoe UI", 9f);
                }

                ApplyToChildren(form, tokens, settings);
            }
            finally
            {
                form.ResumeLayout(false);
            }

            try
            {
                ModernPalette.ApplyDwmStyling(form.Handle, tokens.IsDark);
            }
            catch
            {
                // DWM attributes are best effort on older Windows builds.
            }
        }

        public static void ApplyMenu(ContextMenuStrip menu, AppSettings? settings = null)
        {
            if (menu == null) return;
            settings ??= CurrentSettings;

            if (!UseCustomStyling(settings))
            {
                menu.Renderer = new ToolStripProfessionalRenderer();
                menu.BackColor = SystemColors.Control;
                menu.ForeColor = SystemColors.ControlText;
                return;
            }

            bool dark = IsDark(settings);
            menu.Renderer = new ModernMenuRenderer(dark);
            menu.BackColor = dark ? Color.FromArgb(32, 32, 36) : Color.FromArgb(250, 250, 252);
            menu.ForeColor = dark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
            menu.ShowImageMargin = false;

            foreach (ToolStripItem item in menu.Items)
            {
                StyleMenuItem(item, dark);
            }
        }

        private static void StyleMenuItem(ToolStripItem item, bool dark)
        {
            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.ForeColor = dark ? ModernPalette.DarkTextPrimary : ModernPalette.LightTextPrimary;
                foreach (ToolStripItem child in menuItem.DropDownItems)
                {
                    StyleMenuItem(child, dark);
                }
            }
        }

        public static void StyleButton(Button button, bool primary, AppSettings? settings = null)
        {
            if (button == null) return;
            settings ??= CurrentSettings;
            var tokens = GetTokens(settings);

            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = tokens.Border;
            button.BackColor = primary ? tokens.Accent : tokens.CardBg;
            button.ForeColor = primary ? tokens.AccentText : tokens.TextPrimary;
            button.Cursor = Cursors.Hand;
        }

        private static void ApplyNativeForm(Form form)
        {
            form.SuspendLayout();
            try
            {
                form.BackColor = SystemColors.Control;
                form.ForeColor = SystemColors.ControlText;
                ApplyNativeToChildren(form);
            }
            finally
            {
                form.ResumeLayout(false);
            }
        }

        private static void ApplyNativeToChildren(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                switch (control)
                {
                    case TextBoxBase textBox:
                        textBox.BackColor = SystemColors.Window;
                        textBox.ForeColor = SystemColors.WindowText;
                        break;
                    case ComboBox combo:
                        combo.BackColor = SystemColors.Window;
                        combo.ForeColor = SystemColors.WindowText;
                        combo.FlatStyle = FlatStyle.Standard;
                        break;
                    case Button button:
                        button.BackColor = SystemColors.Control;
                        button.ForeColor = SystemColors.ControlText;
                        button.FlatStyle = FlatStyle.Standard;
                        break;
                    case CheckBox checkBox:
                        checkBox.BackColor = SystemColors.Control;
                        checkBox.ForeColor = SystemColors.ControlText;
                        break;
                    case RadioButton radio:
                        radio.BackColor = SystemColors.Control;
                        radio.ForeColor = SystemColors.ControlText;
                        break;
                    case LinkLabel link:
                        link.LinkColor = SystemColors.HotTrack;
                        break;
                    case Label label:
                        label.BackColor = Color.Transparent;
                        label.ForeColor = SystemColors.ControlText;
                        break;
                    case ListView listView:
                        listView.BackColor = SystemColors.Window;
                        listView.ForeColor = SystemColors.WindowText;
                        break;
                    case ListBox listBox:
                        listBox.BackColor = SystemColors.Window;
                        listBox.ForeColor = SystemColors.WindowText;
                        break;
                    case TabPage tabPage:
                        tabPage.BackColor = SystemColors.Control;
                        tabPage.ForeColor = SystemColors.ControlText;
                        break;
                    case Panel panel:
                        panel.BackColor = SystemColors.Control;
                        break;
                    case GroupBox groupBox:
                        groupBox.BackColor = SystemColors.Control;
                        groupBox.ForeColor = SystemColors.ControlText;
                        break;
                    case TabControl tabControl:
                        tabControl.BackColor = SystemColors.Control;
                        tabControl.ForeColor = SystemColors.ControlText;
                        break;
                }

                if (control.ContextMenuStrip != null)
                {
                    control.ContextMenuStrip.Renderer = new ToolStripProfessionalRenderer();
                }

                if (control.HasChildren)
                {
                    ApplyNativeToChildren(control);
                }
            }
        }

        private static void ApplyToChildren(Control parent, UiStyleTokens tokens, AppSettings settings)
        {
            foreach (Control control in parent.Controls)
            {
                ApplyToControl(control, tokens, settings);

                if (control.ContextMenuStrip != null)
                {
                    ApplyMenu(control.ContextMenuStrip, settings);
                }

                if (control.HasChildren)
                {
                    ApplyToChildren(control, tokens, settings);
                }
            }
        }

        private static void ApplyToControl(Control control, UiStyleTokens tokens, AppSettings settings)
        {
            // Opt out hook for surfaces that paint themselves.
            if (control.Tag is string tag && tag.Equals("no-style", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            switch (control)
            {
                case RichTextBox richTextBox:
                    richTextBox.BackColor = tokens.CardBg;
                    richTextBox.ForeColor = tokens.TextPrimary;
                    break;

                case TextBoxBase textBox:
                    textBox.BackColor = tokens.InputBg;
                    textBox.ForeColor = tokens.TextPrimary;
                    break;

                case ComboBox combo:
                    combo.BackColor = tokens.InputBg;
                    combo.ForeColor = tokens.TextPrimary;
                    combo.FlatStyle = FlatStyle.Flat;
                    break;

                case Button button:
                    // Preserve explicitly colored accent/danger buttons.
                    if (IsDefaultBackColor(button.BackColor))
                    {
                        button.BackColor = tokens.CardBg;
                        button.ForeColor = tokens.TextPrimary;
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderSize = 1;
                        button.FlatAppearance.BorderColor = tokens.Border;
                        button.Cursor = Cursors.Hand;
                    }
                    else if (IsDefaultForeColor(button.ForeColor))
                    {
                        button.ForeColor = tokens.TextPrimary;
                    }
                    break;

                case CheckBox checkBox:
                    if (IsDefaultBackColor(checkBox.BackColor)) checkBox.BackColor = Color.Transparent;
                    if (IsDefaultForeColor(checkBox.ForeColor)) checkBox.ForeColor = tokens.TextPrimary;
                    break;

                case RadioButton radio:
                    if (IsDefaultBackColor(radio.BackColor)) radio.BackColor = Color.Transparent;
                    if (IsDefaultForeColor(radio.ForeColor)) radio.ForeColor = tokens.TextPrimary;
                    break;

                case LinkLabel link:
                    if (IsDefaultBackColor(link.BackColor)) link.BackColor = Color.Transparent;
                    link.LinkColor = tokens.Accent;
                    link.ActiveLinkColor = tokens.Accent;
                    break;

                case Label label:
                    if (IsDefaultBackColor(label.BackColor)) label.BackColor = Color.Transparent;
                    if (IsDefaultForeColor(label.ForeColor)) label.ForeColor = tokens.TextPrimary;
                    break;

                case ListView listView:
                    listView.BackColor = tokens.CardBg;
                    listView.ForeColor = tokens.TextPrimary;
                    listView.BorderStyle = BorderStyle.None;
                    break;

                case CheckedListBox checkedList:
                    checkedList.BackColor = tokens.CardBg;
                    checkedList.ForeColor = tokens.TextPrimary;
                    checkedList.BorderStyle = BorderStyle.None;
                    break;

                case ListBox listBox:
                    listBox.BackColor = tokens.CardBg;
                    listBox.ForeColor = tokens.TextPrimary;
                    listBox.BorderStyle = BorderStyle.None;
                    break;

                case GroupBox groupBox:
                    groupBox.BackColor = Color.Transparent;
                    groupBox.ForeColor = tokens.TextPrimary;
                    break;

                case TabControl tabControl:
                    tabControl.BackColor = tokens.WindowBg;
                    tabControl.ForeColor = tokens.TextPrimary;
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = tokens.WindowBg;
                    tabPage.ForeColor = tokens.TextPrimary;
                    break;

                case FlowLayoutPanel flow:
                    if (IsDefaultBackColor(flow.BackColor)) flow.BackColor = tokens.WindowBg;
                    break;

                case TableLayoutPanel table:
                    if (IsDefaultBackColor(table.BackColor)) table.BackColor = tokens.WindowBg;
                    break;

                case Panel panel:
                    if (IsDefaultBackColor(panel.BackColor)) panel.BackColor = tokens.WindowBg;
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor = tokens.InputBg;
                    numeric.ForeColor = tokens.TextPrimary;
                    break;

                case DataGridView grid:
                    grid.BackgroundColor = tokens.WindowBg;
                    grid.GridColor = tokens.Border;
                    grid.DefaultCellStyle.BackColor = tokens.CardBg;
                    grid.DefaultCellStyle.ForeColor = tokens.TextPrimary;
                    grid.DefaultCellStyle.SelectionBackColor = tokens.Accent;
                    grid.DefaultCellStyle.SelectionForeColor = tokens.AccentText;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = tokens.InputBg;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = tokens.TextPrimary;
                    grid.EnableHeadersVisualStyles = false;
                    break;
            }
        }

        private static bool IsDefaultBackColor(Color color) =>
            color.IsEmpty ||
            color == Color.Transparent ||
            color == SystemColors.Control ||
            color == SystemColors.Window;

        private static bool IsDefaultForeColor(Color color) =>
            color.IsEmpty ||
            color == SystemColors.ControlText ||
            color == SystemColors.WindowText;
    }
}
