namespace SharedBase.Tests.ModelVerifiers.Tests;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;
using Xunit;

public class NoWhitespaceAttributeTests
{
    [Theory]
    [InlineData("aSingleWord")]
    [InlineData("https://revolutionarygamesstudio.com")]
    [InlineData("")]
    [InlineData(null)]
    public void NoWhitespace_AllowsValid(string? uri)
    {
        var model = new Model1
        {
            TextProperty = uri,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("just spaces")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("and trailing ")]
    public void NoWhitespace_DisallowsInvalid(string uri)
    {
        var model = new Model1
        {
            TextProperty = uri,
        };

        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model1.TextProperty), errors[0].MemberNames);
    }

    private class Model1
    {
        [NoWhitespace]
        public string? TextProperty { get; set; }
    }
}
