using ClipboardApp.Core.Models;
using ClipboardApp.WinForms.Controls;
using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace ClipboardApp.Tests
{
    public class CardGestureTests
    {
        private static readonly MethodInfo MouseDown = typeof(ModernCardList)
            .GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo MouseUp = typeof(ModernCardList)
            .GetMethod("OnMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo MouseMove = typeof(ModernCardList)
            .GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!;

        [Fact]
        public void DoubleClickRaisesCardActivatedInsteadOfDrag()
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                    var list = new ModernCardList { Dock = DockStyle.Fill, DragEnabled = true, DragArmed = true };
                    form.Controls.Add(list);
                    form.CreateControl();
                    list.CreateControl();

                    var entry = new ClipboardEntry { Content = "double-click target" };
                    list.Items.Add(entry);

                    ClipboardEntry? activated = null;
                    int activationCount = 0;
                    list.CardActivated += (_, e) => { activated = e; activationCount++; };

                    var itemRect = list.GetItemRectangle(0);
                    int x = itemRect.X + 80;
                    int y = itemRect.Y + (itemRect.Height / 2);

                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    MouseUp.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    // Well inside the Windows double-click interval.
                    Thread.Sleep(60);
                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    MouseUp.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });

                    Assert.Equal(1, activationCount);
                    Assert.NotNull(activated);
                    Assert.Same(entry, activated);
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
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "STA gesture test timed out.");
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void CheckboxClickTogglesWithoutStartingDoubleClickPaste()
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                    var list = new ModernCardList { Dock = DockStyle.Fill };
                    form.Controls.Add(list);
                    form.CreateControl();
                    list.CreateControl();

                    var entry = new ClipboardEntry { Content = "checkbox target" };
                    list.Items.Add(entry);

                    ClipboardEntry? activated = null;
                    list.CardActivated += (_, e) => activated = e;

                    var itemRect = list.GetItemRectangle(0);
                    int x = itemRect.X + 20; // checkbox area
                    int y = itemRect.Y + (itemRect.Height / 2);

                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    MouseUp.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });

                    Assert.Contains(entry.Id, list.GetCheckedIds());
                    Assert.Null(activated);
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
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "STA gesture test timed out.");
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void DragOnlyStartsWhenArmed()
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                    var list = new ModernCardList { Dock = DockStyle.Fill, DragEnabled = true, DragArmed = false };
                    form.Controls.Add(list);
                    form.CreateControl();
                    list.CreateControl();

                    list.Items.Add(new ClipboardEntry { Content = "drag target" });

                    bool dragStarted = false;
                    list.StartCardDragRequested += (_, _) => dragStarted = true;

                    var rect = list.GetItemRectangle(0);
                    int x = rect.X + 80;
                    int y = rect.Y + (rect.Height / 2);

                    // Unarmed: movement must not start a drag.
                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    MouseMove.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 0, x + 60, y, 0) });
                    MouseUp.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x + 60, y, 0) });
                    Assert.False(dragStarted);

                    list.ResetInteractionState();
                    list.DragArmed = true;

                    // Armed: the same gesture starts one drag.
                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x, y, 0) });
                    MouseMove.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 0, x + 60, y, 0) });

                    Assert.True(dragStarted);
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
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "STA gesture test timed out.");
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void FastClicksOnDifferentCardsDoNotActivate()
        {
            Exception? failure = null;
            var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    using var form = new Form { Width = 500, Height = 400, ShowInTaskbar = false };
                    var list = new ModernCardList { Dock = DockStyle.Fill };
                    form.Controls.Add(list);
                    form.CreateControl();
                    list.CreateControl();

                    list.Items.Add(new ClipboardEntry { Content = "first" });
                    list.Items.Add(new ClipboardEntry { Content = "second" });

                    ClipboardEntry? activated = null;
                    list.CardActivated += (_, e) => activated = e;

                    var first = list.GetItemRectangle(0);
                    var second = list.GetItemRectangle(1);
                    int x1 = first.X + 80, y1 = first.Y + first.Height / 2;
                    int x2 = second.X + 80, y2 = second.Y + second.Height / 2;

                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x1, y1, 0) });
                    MouseUp.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, x1, y1, 0) });
                    Thread.Sleep(30);
                    MouseDown.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 2, x2, y2, 0) });

                    Assert.Null(activated);
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
            Assert.True(completed.Wait(TimeSpan.FromSeconds(10)), "STA gesture test timed out.");
            thread.Join();
            Assert.Null(failure);
        }
    }
}
