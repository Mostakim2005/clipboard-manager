using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.UI;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public class TagDialog : Form
    {
        public new Tag? Tag { get; private set; }
        private readonly TextBox _nameBox;
        private readonly ComboBox _colorBox;

        public TagDialog(AppSettings? settings = null)
        {
            Text = "New Tag";
            Width = 300; Height = 150;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var lbl = new Label { Text = "Tag Name:", Dock = DockStyle.Top };
            _nameBox = new TextBox { Dock = DockStyle.Top };
            var colorLbl = new Label { Text = "Color:", Dock = DockStyle.Top };
            _colorBox = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
            _colorBox.Items.AddRange(new object[] { "Red", "Blue", "Green", "Orange", "Purple" });
            _colorBox.SelectedIndex = 0;
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom };
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { cancel, ok, _colorBox, colorLbl, _nameBox, lbl });
            UiStyler.ApplyForm(this, settings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                var color = _colorBox.SelectedItem?.ToString() ?? "Blue";
                Tag = new Tag { Name = _nameBox.Text.Trim(), Color = color };
            }
            base.OnFormClosing(e);
        }
    }
}
