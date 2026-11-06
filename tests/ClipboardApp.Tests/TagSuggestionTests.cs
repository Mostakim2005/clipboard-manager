using ClipboardApp.Core.Models;
using ClipboardApp.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace ClipboardApp.Tests
{
    public class TagSuggestionTests
    {
        [Fact]
        public void CollectTagsRanksFrequentValuesFirst()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Tags = new List<string> { "prod", "api" } },
                new() { Tags = new List<string> { "prod", "payments" } },
                new() { Tags = new List<string> { "prod, api", "#work" } }
            };

            var tags = TagSuggestionService.CollectTags(entries);

            Assert.Equal("prod", tags[0]);
            Assert.Contains("api", tags);
            Assert.Contains("payments", tags);
            Assert.Contains("work", tags);
        }

        [Fact]
        public void CollectAliasesIgnoresBlankValues()
        {
            var entries = new List<ClipboardEntry>
            {
                new() { Alias = "deploy command" },
                new() { Alias = "  " },
                new() { Alias = "deploy command" },
                new() { Alias = "api base url" }
            };

            var aliases = TagSuggestionService.CollectAliases(entries);

            Assert.Equal("deploy command", aliases[0]);
            Assert.Contains("api base url", aliases);
            Assert.DoesNotContain("", aliases);
        }

        [Fact]
        public void SuggestPutsPrefixMatchesBeforeContainsMatches()
        {
            var source = new[] { "payments", "prepaid", "api" };

            var result = TagSuggestionService.Suggest(source, "pa", 8);

            Assert.Equal("payments", result[0]);
            Assert.Contains("prepaid", result);
            Assert.DoesNotContain("api", result);
        }

        [Fact]
        public void SuggestIsCaseInsensitive()
        {
            var result = TagSuggestionService.Suggest(new[] { "Production" }, "prod", 8);

            Assert.Single(result);
            Assert.Equal("Production", result[0]);
        }

        [Fact]
        public void GetTokenStopsAtSeparatorsAndIgnoresLeadingSpace()
        {
            string text = "alpha,  beta";

            string token = TagSuggestionService.GetToken(
                text,
                text.Length,
                new[] { ',', ';' },
                out int start,
                out int length);

            Assert.Equal("beta", token);
            Assert.Equal(8, start);
            Assert.Equal(4, length);
        }

        [Fact]
        public void ApplySuggestionReplacesOnlyTheCurrentToken()
        {
            string text = "alpha, be";

            string updated = TagSuggestionService.ApplySuggestion(text, 7, 2, "beta");

            Assert.Equal("alpha, beta", updated);
        }

        [Fact]
        public void EmptyTokenDoesNotSuggestEverything()
        {
            var result = TagSuggestionService.GetToken(
                "alpha, ",
                7,
                new[] { ',' },
                out _,
                out _);

            Assert.Equal("", result);
        }
    }
}
