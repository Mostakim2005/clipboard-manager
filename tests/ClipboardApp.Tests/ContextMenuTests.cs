using ClipboardApp.WinForms.UI;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ClipboardApp.Tests
{
    public class ContextMenuTests
    {
        [Fact]
        public void NormalizeOrderKeepsKnownKeysAndAppendsMissingOnes()
        {
            var order = ContextMenuCommands.NormalizeOrder(new[] { "delete", "copy", "delete", "unknown" });

            Assert.Equal(ContextMenuCommands.All.Count, order.Count);
            Assert.Equal("delete", order[0]);
            Assert.Equal("copy", order[1]);
            Assert.Equal(ContextMenuCommands.CustomizeKey, order[^1]);
            Assert.Equal(order.Count, order.Distinct().Count());
        }

        [Fact]
        public void CustomizeCommandCannotBeHidden()
        {
            var hidden = new[] { ContextMenuCommands.CustomizeKey, "pin" };

            Assert.False(ContextMenuCommands.IsHidden(hidden, ContextMenuCommands.CustomizeKey));
            Assert.True(ContextMenuCommands.IsHidden(hidden, "pin"));
        }

        [Fact]
        public void NormalizeHiddenDropsUnknownAndNonHideableKeys()
        {
            var hidden = ContextMenuCommands.NormalizeHidden(new[]
            {
                "PIN", "unknown", ContextMenuCommands.CustomizeKey, "pin"
            });

            Assert.Single(hidden);
            Assert.Equal("pin", hidden[0]);
        }

        [Fact]
        public void VisibleKeysHonorsPersistedOrderAndHiddenList()
        {
            var visible = ContextMenuCommands.VisibleKeys(
                new[] { "paste", "copy", "delete" },
                new[] { "delete" });

            Assert.Equal("paste", visible[0]);
            Assert.Equal("copy", visible[1]);
            Assert.DoesNotContain("delete", visible);
            Assert.Equal(ContextMenuCommands.CustomizeKey, visible[^1]);
        }

        [Fact]
        public void EveryRegisteredKeyIsUniqueAndHasALabel()
        {
            Assert.Equal(
                ContextMenuCommands.All.Count,
                ContextMenuCommands.All.Select(c => c.Key).Distinct().Count());

            Assert.All(ContextMenuCommands.All, command =>
            {
                Assert.False(string.IsNullOrWhiteSpace(command.Key));
                Assert.False(string.IsNullOrWhiteSpace(command.Label));
                Assert.False(string.IsNullOrWhiteSpace(command.Description));
            });
        }
    }
}
