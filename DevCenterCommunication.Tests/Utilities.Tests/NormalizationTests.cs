namespace DevCenterCommunication.Tests.Utilities.Tests;

using System;
using DevCenterCommunication.Utilities;
using Xunit;

public class NormalizationTests
{
    [Theory]
    [InlineData("username", "username")]
    [InlineData("user name", "user_name")]
    [InlineData("user_name", "user_name")]
    [InlineData("user@name", "user-name")]
    [InlineData(" whitespace  ", "whitespace")]
    [InlineData("whitespace\n", "whitespace")]
    [InlineData("allowed_-.1AZz", "allowed_-.1AZz")]
    [InlineData("no/slash", "no-slash")]
    [InlineData("Mr. Patron", "Mr._Patron")]
    [InlineData("hi", "hii")]
    [InlineData("a_username_that_is_way_too_long_to_be_acceptable_in_any_reasonable_system_whatsoever",
        "a_username_that_is_way_too_long_to_be_acceptable_i")]
    public void Normalization_UserNameNormalizationWorks(string raw, string expected)
    {
        Assert.Equal(expected, Normalization.NormalizeUserName(raw));
    }

    [Theory]
    [InlineData("test@example.com", "test@example.com")]
    [InlineData("test+spam1@example.com", "test@example.com")]
    [InlineData("mr.test@example.com", "mrtest@example.com")]
    [InlineData("MrTest@example.com", "mrtest@example.com")]
    [InlineData("a.lot.of.Dots@example.com", "alotofdots@example.com")]
    public void Normalization_EmailNormalizationWorks(string raw, string expected)
    {
        Assert.Equal(expected, Normalization.NormalizeEmail(raw));
    }

    [Fact]
    public void Normalization_EmailRequiresAt()
    {
        Assert.Throws<ArgumentException>(() => Normalization.NormalizeEmail("some.almost-email"));
    }
}
