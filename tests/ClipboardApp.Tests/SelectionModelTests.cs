using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    public class SelectionModelTests
    {
        private static readonly MethodInfo KeyDown = typeof(ModernCardList)
            .GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Fact]
        public void CtrlArrowAddsProgressively_ShiftArrowSelectsRange()
        {
            RunOnSta(() =>
            {
                using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                var list = new ModernCardList { Dock = DockStyle.Fill, ShowCheckboxes = true };
                form.Controls.Add(list);
                form.CreateControl();
                list.CreateControl();

                var entries = Enumerable.Range(0, 4)
                    .Select(i => new ClipboardEntry { Content = "entry " + i })
                    .ToList();
                foreach (var entry in entries) list.Items.Add(entry);
                list.SelectedIndex = 0;

                KeyDown.Invoke(list, new object[] { new KeyEventArgs(Keys.Control | Keys.Down) });
                Assert.Equal(2, list.GetCheckedIds().Count);
                Assert.Contains(entries[0].Id, list.GetCheckedIds());
                Assert.Contains(entries[1].Id, list.GetCheckedIds());

                KeyDown.Invoke(list, new object[] { new KeyEventArgs(Keys.Control | Keys.Down) });
                Assert.Equal(3, list.GetCheckedIds().Count);

                // A plain arrow resets the range anchor without clearing checks.
                KeyDown.Invoke(list, new object[] { new KeyEventArgs(Keys.Up) });
                KeyDown.Invoke(list, new object[] { new KeyEventArgs(Keys.Shift | Keys.Down) });
                Assert.Contains(entries[1].Id, list.GetCheckedIds());
                Assert.Contains(entries[2].Id, list.GetCheckedIds());
            });
        }

        [Fact]
        public void SelectionSurvivesRefreshAndOnlyDeletedIdsArePruned()
        {
            RunOnSta(() =>
            {
                using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                var list = new ModernCardList { Dock = DockStyle.Fill };
                form.Controls.Add(list);
                form.CreateControl();
                list.CreateControl();

                var entries = Enumerable.Range(0, 3)
                    .Select(i => new ClipboardEntry { Content = "entry " + i })
                    .ToList();
                foreach (var entry in entries) list.Items.Add(entry);
                list.SelectedIndex = 0;
                KeyDown.Invoke(list, new object[] { new KeyEventArgs(Keys.Control | Keys.A) });
                Assert.Equal(3, list.GetCheckedIds().Count);

                // Simulate a filter that hides two entries; they must stay selected.
                list.PruneCheckedToExisting(entries.Select(e => e.Id));
                Assert.Equal(3, list.GetCheckedIds().Count);

                // Simulate a database delete.
                list.PruneCheckedToExisting(new[] { entries[0].Id });
                Assert.Single(list.GetCheckedIds());
                Assert.Contains(entries[0].Id, list.GetCheckedIds());
            });
        }

        [Fact]
        public void ToggleGroupChecked_SelectsAndDeselectsEntireGroupIndividually()
        {
            RunOnSta(() =>
            {
                using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                var list = new ModernCardList { Dock = DockStyle.Fill, ShowCheckboxes = true };
                form.Controls.Add(list);
                form.CreateControl();
                list.CreateControl();

                var edge1 = new ClipboardEntry { Content = "edge 1", SourceApp = "msedge" };
                var edge2 = new ClipboardEntry { Content = "edge 2", SourceApp = "msedge" };
                var anti1 = new ClipboardEntry { Content = "anti 1", SourceApp = "Antigravity" };
                var anti2 = new ClipboardEntry { Content = "anti 2", SourceApp = "Antigravity" };

                var groupEdge = new CardGroupHeader { Title = "msedge", Count = 2, Key = "msedge" };
                groupEdge.EntryIds.Add(edge1.Id);
                groupEdge.EntryIds.Add(edge2.Id);

                var groupAnti = new CardGroupHeader { Title = "Antigravity", Count = 2, Key = "Antigravity" };
                groupAnti.EntryIds.Add(anti1.Id);
                groupAnti.EntryIds.Add(anti2.Id);

                list.Items.Add(groupEdge);
                list.Items.Add(edge1);
                list.Items.Add(edge2);
                list.Items.Add(groupAnti);
                list.Items.Add(anti1);
                list.Items.Add(anti2);

                // Initially 0 checked
                Assert.Empty(list.GetCheckedIds());

                // Toggle group Edge -> checks both edge1 and edge2, but leaves anti untouched!
                list.ToggleGroupChecked(groupEdge);
                Assert.Equal(2, list.GetCheckedIds().Count);
                Assert.Contains(edge1.Id, list.GetCheckedIds());
                Assert.Contains(edge2.Id, list.GetCheckedIds());
                Assert.DoesNotContain(anti1.Id, list.GetCheckedIds());

                // Toggle group Anti -> now all 4 are checked
                list.ToggleGroupChecked(groupAnti);
                Assert.Equal(4, list.GetCheckedIds().Count);

                // Toggle group Edge again -> unchecks edge1 and edge2, keeping anti checked!
                list.ToggleGroupChecked(groupEdge);
                Assert.Equal(2, list.GetCheckedIds().Count);
                Assert.DoesNotContain(edge1.Id, list.GetCheckedIds());
                Assert.Contains(anti1.Id, list.GetCheckedIds());
                Assert.Contains(anti2.Id, list.GetCheckedIds());
            });
        }

        private static void RunOnSta(Action action)
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
                finally { completed.Set(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "STA selection test timed out.");
            thread.Join();
            Assert.Null(failure);
        }
    }
}
