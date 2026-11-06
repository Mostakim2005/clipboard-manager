using ClipboardApp.WinForms;
using ClipboardApp.WinForms.Dialogs;
using ClipboardApp.WinForms.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    public class UiFeatureSmokeTests
    {
        private static List<Control> Walk(Control root)
        {
            var controls = new List<Control>();
            void Visit(Control parent)
            {
                foreach (Control child in parent.Controls)
                {
                    controls.Add(child);
                    Visit(child);
                }
            }
            Visit(root);
            return controls;
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
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
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "UI smoke test timed out.");
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void NewFeatureTogglesDefaultToSafeValues()
        {
            var settings = new AppSettings();

            Assert.False(settings.EnableDoubleClickPaste);
            Assert.False(settings.EnableMixedMerge);
            Assert.True(settings.EnableCustomUiStyling);
            Assert.Equal("EnterOnly", settings.PasteTrigger);
            Assert.Equal("Fluent", settings.VisualStyle);
            Assert.Equal("Any", settings.FilterDate);
            Assert.Equal("Any", settings.FilterType);
            Assert.Equal("Any", settings.FilterApp);
        }

        [Fact]
        public void OptionsDialogExposesNewTogglesStylesAndCustomizeButton()
        {
            RunSta(() =>
            {
                var settings = new AppSettings { Theme = "Light", VisualStyle = "Fluent" };
                using var dialog = new OptionsDialog(settings);
                dialog.CreateControl();
                dialog.PerformLayout();

                // Only the active section is parented to the dialog at a time,
                // so inspect the cached section panels directly.
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var controls = new List<Control>();
                foreach (string fieldName in new[]
                         {
                             "_pnlGeneral", "_pnlAppearance", "_pnlInteractions", "_pnlPrivacy"
                         })
                {
                    if (typeof(OptionsDialog).GetField(fieldName, flags)?.GetValue(dialog) is Control panel)
                    {
                        controls.Add(panel);
                        controls.AddRange(Walk(panel));
                    }
                }

                var checkboxes = controls.OfType<CheckBox>().Select(c => c.Text).ToList();
                Assert.True(
                    checkboxes.Any(t => t.Contains("Enable double-click paste")),
                    "Checkboxes: " + string.Join(" | ", checkboxes));
                Assert.Contains(checkboxes, t => t.Contains("mixed text + image merging"));
                Assert.Contains(checkboxes, t => t.Contains("Apply custom styling"));

                var buttons = controls.OfType<Button>().Select(b => b.Text).ToList();
                Assert.Contains(buttons, t => t.Contains("Customize right-click menu"));

                var comboValues = controls.OfType<ComboBox>()
                    .SelectMany(c => c.Items.Cast<object>().Select(i => i?.ToString() ?? ""))
                    .ToList();
                Assert.Contains("Native", comboValues);
                Assert.Contains("Rich", comboValues);
            });
        }

        [Fact]
        public void ContextMenuCustomizerListsHideableCommandsAndAlwaysKeepsCustomize()
        {
            RunSta(() =>
            {
                var settings = new AppSettings
                {
                    Theme = "Light",
                    ContextMenuOrder = new List<string>(),
                    ContextMenuHidden = new List<string> { "pin", ContextMenuCommands.CustomizeKey }
                };

                using var dialog = new ContextMenuCustomizeDialog(settings);
                dialog.CreateControl();
                dialog.PerformLayout();

                var controls = Walk(dialog);
                var list = controls.OfType<CheckedListBox>().Single();
                Assert.Equal(ContextMenuCommands.All.Count - 1, list.Items.Count);
                Assert.DoesNotContain(list.Items.Cast<object>(), i => i.ToString() == "Customize this menu...");

                var buttons = controls.OfType<Button>().Select(b => b.Text).ToList();
                Assert.Contains("Save", buttons);
                Assert.Contains("Move Up", buttons);
                Assert.Contains("Move Down", buttons);
                Assert.Contains("Show All", buttons);
            });
        }

        [Fact]
        public void ThemedMessageDialogCarriesTextAndButtons()
        {
            RunSta(() =>
            {
                using var dialog = new MessageDialog(
                    "Delete this clip?",
                    "Confirm",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                dialog.CreateControl();
                dialog.PerformLayout();

                var controls = Walk(dialog);
                Assert.Contains(controls.OfType<Label>(), l => l.Text == "Delete this clip?");
                var buttons = controls.OfType<Button>().Select(b => b.Text).ToList();
                Assert.Contains("Yes", buttons);
                Assert.Contains("No", buttons);
            });
        }
    }
}
