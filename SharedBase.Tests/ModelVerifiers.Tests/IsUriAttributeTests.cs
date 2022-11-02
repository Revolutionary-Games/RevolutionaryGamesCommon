namespace SharedBase.Tests.ModelVerifiers.Tests;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;
using Xunit;

public class IsUriAttributeTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("https://revolutionarygamesstudio.com")]
    [InlineData("http://example.com")]
    [InlineData(null)]
    [InlineData("")]
    public void IsUri_AllowsValid(string uri)
    {
        var model = new Model1
        {
            UriProperty = uri,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("test")]
    [InlineData("just some stuff")]
    [InlineData("https://revolutionarygamesstudio.com a")]
    public void IsUri_DisallowsInvalid(string uri)
    {
        var model = new Model1
        {
            UriProperty = uri,
        };

        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model1.UriProperty), errors[0].MemberNames);
    }

    private class Model1
    {
        [IsUri]
        public string? UriProperty { get; set; }
    }
}
