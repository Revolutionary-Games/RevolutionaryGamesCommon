namespace SharedBase.Tests.Utilities.Tests;

using System;
using SharedBase.Utilities;
using Xunit;

public class UriExtensionsTests
{
    [Theory]
    [InlineData("https://github.com/test?value=123", "https://github.com/test")]
    [InlineData("https://github.com/test", "https://github.com/test")]
    [InlineData("https://github.com/test?", "https://github.com/test")]
    [InlineData("https://github.com/test?a", "https://github.com/test")]
    public void UriExtension_ProperlyCutsQuery(string uriString, string expected)
    {
        var result = uriString.UriWithoutQuery();

        Assert.Equal(expected, result);

        var asUri = new Uri(uriString);

        Assert.Equal(expected, asUri.WithoutQuery());
    }
}
