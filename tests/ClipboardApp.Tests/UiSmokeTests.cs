using ClipboardApp.WinForms;
using ClipboardApp.WinForms.Dialogs;
using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    public class UiSmokeTests
    {
        [Fact]
        public void OptionsDialogConstructsWithAllTogglesAndHelpPanel()
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    var settings = new AppSettings
                    {
                        Theme = "Light",
                        LiveRefresh = true,
                        RequireManualDragArm = true,
                        SequentialBatchPaste = false
                    };

                    using var dialog = new OptionsDialog(settings);
                    dialog.CreateControl();
                    dialog.PerformLayout();

                    Assert.Same(settings, dialog.Settings);
                    Assert.True(dialog.Width > 500);
                    Assert.True(dialog.Height > 400);

                    var controls = new System.Collections.Generic.List<Control>();
                    void Walk(Control parent)
                    {
                        foreach (Control child in parent.Controls)
                        {
                            controls.Add(child);
                            Walk(child);
                        }
                    }
                    Walk(dialog);

                    var checkboxTexts = controls.OfType<CheckBox>().Select(c => c.Text).ToList();
                    Assert.Contains(checkboxTexts, t => t.Contains("FD: fresh data"));
                    Assert.Contains(checkboxTexts, t => t.Contains("File Drop / FD"));
                    Assert.Contains(checkboxTexts, t => t.Contains("Refresh list after a confirmed copy"));

                    var labels = controls.OfType<Label>().Select(l => l.Text).ToList();
                    Assert.Contains(labels, t => t.Contains("Hover or focus any setting"));

                    var helpLabel = controls.OfType<Label>().First(l => l.Text.Contains("Hover or focus any setting"));
                    Assert.Equal(DockStyle.Bottom, helpLabel.Parent!.Dock);
                    Assert.True(helpLabel.Parent.Height >= 100);

                    var saveButton = controls.OfType<Button>().First(b => b.Text == "Save");
                    var bottomBar = saveButton.Parent!;
                    Assert.True(helpLabel.Parent.Bottom <= bottomBar.Top + 1,
                        $"Help panel {helpLabel.Parent.Bounds} overlaps bottom bar {bottomBar.Bounds}.");
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "Options dialog smoke test timed out.");
            thread.Join();
            Assert.Null(failure);
        }
    }
}
