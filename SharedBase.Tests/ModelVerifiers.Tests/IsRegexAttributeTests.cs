namespace SharedBase.Tests.ModelVerifiers.Tests;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;
using Xunit;

public class IsRegexAttributeTests
{
    [Theory]
    [InlineData("a")]
    [InlineData("just a thing")]
    [InlineData("some (regex)+stuff\\s here.*")]
    [InlineData(null)]
    public void IsRegex_AllowsValid(string regex)
    {
        var model = new Model1
        {
            Regex = regex,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("(")]
    [InlineData("[ab")]
    [InlineData("\\")]
    [InlineData("")]
    public void IsRegex_DisallowsInvalid(string invalidRegex)
    {
        var model = new Model1
        {
            Regex = invalidRegex,
        };

        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model1.Regex), errors[0].MemberNames);
    }

    [Fact]
    public void IsRegex_AllowsBlankInSpecificMode()
    {
        var model = new Model2
        {
            Regex = string.Empty,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    private class Model1
    {
        [IsRegex]
        public string? Regex { get; set; }
    }

    private class Model2
    {
        [IsRegex(AllowBlank = true)]

        // Read by the property
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string? Regex { get; set; }
    }
}
