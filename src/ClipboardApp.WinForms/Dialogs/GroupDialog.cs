using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.UI;
using System.Windows.Forms;

namespace ClipboardApp.WinForms.Dialogs
{
    public class GroupDialog : Form
    {
        public ClipboardGroup? Group { get; private set; }
        private readonly TextBox _nameBox;

        public GroupDialog(AppSettings? settings = null)
        {
            Text = "New Group";
            Width = 300; Height = 130;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;

            var lbl = new Label { Text = "Group Name:", Dock = DockStyle.Top };
            _nameBox = new TextBox { Dock = DockStyle.Top };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Dock = DockStyle.Bottom };
            AcceptButton = ok; CancelButton = cancel;
            Controls.AddRange(new Control[] { cancel, ok, _nameBox, lbl });
            UiStyler.ApplyForm(this, settings);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(_nameBox.Text))
                Group = new ClipboardGroup { Name = _nameBox.Text.Trim() };
            base.OnFormClosing(e);
        }
    }
}
