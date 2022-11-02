namespace SharedBase.Tests.ModelVerifiers.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;
using Xunit;

public class ToStringMaxLengthAttributeTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://revolutionarygamesstudio.com")]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/someReallyLong?")]
    [InlineData(null)]
    public void ToStringMaxLength_AllowsValid(string? uri)
    {
        var model = new Model1
        {
            UriProperty = uri == null ? null : new Uri(uri),
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("https://example.com/someReallyLong/urlPathGoesHereThatIsWayTooLongToPassChecks")]
    [InlineData("https://example.com/someReallyLong?query=urlPathGoesHereThatIsWayTooLongToPassChecks")]
    public void ToStringMaxLength_DisallowsInvalid(string uri)
    {
        var model = new Model1
        {
            UriProperty = new Uri(uri),
        };

        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model1.UriProperty), errors[0].MemberNames);
    }

    private class Model1
    {
        [ToStringMaxLength(50)]
        public Uri? UriProperty { get; set; }
    }
}
