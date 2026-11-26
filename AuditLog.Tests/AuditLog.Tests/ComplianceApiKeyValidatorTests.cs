using AuditLog.API.Auth;

namespace AuditLog.Tests;

public sealed class ComplianceApiKeyValidatorTests
{
    private const string ConfiguredKey =
        "TEST_ONLY_ComplianceApiKeyForUnitTests_0123456789";

    [Fact]
    public void IsValid_AcceptsConfiguredKey()
    {
        Assert.True(ComplianceApiKeyValidator.IsValid(
            ConfiguredKey,
            ConfiguredKey));
    }

    [Fact]
    public void IsValid_RejectsIncorrectOrMissingKey()
    {
        Assert.False(ComplianceApiKeyValidator.IsValid(
            "incorrect-key",
            ConfiguredKey));
        Assert.False(ComplianceApiKeyValidator.IsValid(
            null,
            ConfiguredKey));
        Assert.False(ComplianceApiKeyValidator.IsValid(
            ConfiguredKey,
            null));
    }

    [Fact]
    public void IsValid_RejectsShortConfiguredKey()
    {
        Assert.False(ComplianceApiKeyValidator.IsValid(
            "short-key",
            "short-key"));
    }
}
