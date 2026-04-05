using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MLS.AIHub.Persistence;
using MLS.AIHub.Providers;
using MLS.AIHub.Services;
using MLS.Core.Contracts.Designer;
using Moq;
using System.Text.Json;
using Xunit;

namespace MLS.AIHub.Tests;

/// <summary>Unit tests for <see cref="ProviderRouter"/>.</summary>
public sealed class ProviderRouterTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AiQueryPayload BuildQuery(string? providerOverride = null) => new(
        Query: "What is the current BTC price?",
        ProviderOverride: providerOverride,
        ModelOverride: null,
        IncludeCanvasContext: false,
        ConversationHistory: Array.Empty<JsonElement>());

    private static Mock<ILLMProvider> BuildProvider(string id, bool isAvailable, bool probeResult)
    {
        var mock = new Mock<ILLMProvider>();
        mock.SetupGet(p => p.ProviderId).Returns(id);
        mock.SetupGet(p => p.IsAvailable).Returns(isAvailable);
        mock.Setup(p => p.CheckAvailabilityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(probeResult);
        return mock;
    }

    private static Mock<IUserPreferenceRepository> BuildPrefsWithNoPreference()
    {
        var mock = new Mock<IUserPreferenceRepository>();
        mock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreference?)null);
        return mock;
    }

    private static Mock<IUserPreferenceRepository> BuildPrefsWithPreference(string primaryId, string[] fallbackChain)
    {
        var mock = new Mock<IUserPreferenceRepository>();
        var pref = new UserPreference
        {
            UserId            = Guid.NewGuid(),
            PrimaryProviderId = primaryId,
            FallbackChainRaw  = string.Join(",", fallbackChain),
        };
        mock.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pref);
        return mock;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SelectProvider_FallsBackToLocalWhenPrimaryUnavailable()
    {
        // Arrange — primary "openai" provider is unavailable (circuit-open)
        var openai = BuildProvider("openai", isAvailable: false, probeResult: false);
        var groq   = BuildProvider("groq",   isAvailable: false, probeResult: false);
        var local  = BuildProvider("local",  isAvailable: true,  probeResult: true);

        var prefs = BuildPrefsWithPreference("openai", ["openai", "groq", "local"]);

        var router = new ProviderRouter(
            [openai.Object, groq.Object, local.Object],
            prefs.Object,
            NullLogger<ProviderRouter>.Instance);

        // Act
        var selected = await router.SelectProviderAsync(BuildQuery(), userId: Guid.NewGuid());

        // Assert
        selected.ProviderId.Should().Be("local");
    }

    [Fact]
    public async Task SelectProvider_UsesPerRequestOverrideWhenSpecified()
    {
        // Arrange
        var openai = BuildProvider("openai", isAvailable: true, probeResult: true);
        var groq   = BuildProvider("groq",   isAvailable: true, probeResult: true);

        var router = new ProviderRouter(
            [openai.Object, groq.Object],
            BuildPrefsWithNoPreference().Object,
            NullLogger<ProviderRouter>.Instance);

        // Act
        var selected = await router.SelectProviderAsync(BuildQuery(providerOverride: "groq"), Guid.NewGuid());

        // Assert
        selected.ProviderId.Should().Be("groq");
    }

    [Fact]
    public async Task SelectProvider_UsesUserPreferenceWhenAvailable()
    {
        // Arrange
        var openai = BuildProvider("openai", isAvailable: true, probeResult: true);
        var local  = BuildProvider("local",  isAvailable: true, probeResult: true);

        var prefs = BuildPrefsWithPreference("openai", ["openai", "local"]);

        var router = new ProviderRouter(
            [openai.Object, local.Object],
            prefs.Object,
            NullLogger<ProviderRouter>.Instance);

        // Act
        var selected = await router.SelectProviderAsync(BuildQuery(), Guid.NewGuid());

        // Assert
        selected.ProviderId.Should().Be("openai");
    }

    [Fact]
    public async Task SelectProvider_SkipsUnavailableProvidersInFallbackChain()
    {
        // Arrange — only "anthropic" is available after "openai" fails
        var openai    = BuildProvider("openai",    isAvailable: false, probeResult: false);
        var anthropic = BuildProvider("anthropic", isAvailable: true,  probeResult: true);
        var local     = BuildProvider("local",     isAvailable: true,  probeResult: true);

        var prefs = BuildPrefsWithNoPreference();

        // No user preference → fallback chain from router defaults
        // Override user pref to test fallback chain explicitly
        var prefsWithFallback = BuildPrefsWithPreference("openai", ["openai", "anthropic", "local"]);

        var router = new ProviderRouter(
            [openai.Object, anthropic.Object, local.Object],
            prefsWithFallback.Object,
            NullLogger<ProviderRouter>.Instance);

        // Act
        var selected = await router.SelectProviderAsync(BuildQuery(), Guid.NewGuid());

        // Assert
        selected.ProviderId.Should().Be("anthropic");
    }

    [Fact]
    public async Task SelectProvider_ThrowsWhenNoProvidersRegistered()
    {
        // Arrange
        var router = new ProviderRouter(
            [],
            BuildPrefsWithNoPreference().Object,
            NullLogger<ProviderRouter>.Instance);

        // Act
        var act = async () => await router.SelectProviderAsync(BuildQuery(), Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No LLM provider*");
    }
}
