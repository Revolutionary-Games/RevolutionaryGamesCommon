namespace SharedBase.Tests.ModelVerifiers.Tests;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SharedBase.ModelVerifiers;
using Xunit;

public class MayNotContainAttributeTests
{
    [Fact]
    public void MayNotContain_AllowsNull()
    {
        var model = new Model1
        {
            Property = null,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void MayNotContain_AllowsEmpty()
    {
        var model = new Model1
        {
            Property = string.Empty,
        };

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);
    }

    [Fact]
    public void MayNotContain_StringProperty()
    {
        var model = new Model1();

        var errors = new List<ValidationResult>();

        model.Property = "thing without that letter";

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);

        model.Property = "string with z";

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model1.Property), errors[0].MemberNames);
    }

    [Fact]
    public void MayNotContain_ListOfStringsProperty()
    {
        var model = new Model2(new List<string>());

        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.Empty(errors);

        model.Property = new List<string> { "item", "and other stuff", "third thing" };

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model2.Property), errors[0].MemberNames);

        model.Property = new List<string> { "item" };

        errors.Clear();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void MayNotContain_MultipleValues()
    {
        var model = new Model3();

        var errors = new List<ValidationResult>();

        model.Property = "string with z and b";

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));
        Assert.NotEmpty(errors);

        Assert.NotNull(errors[0].ErrorMessage);
        Assert.Contains(nameof(Model3.Property), errors[0].MemberNames);

        model.Property = "only z";
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));

        model.Property = "only b";
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors));

        model.Property = string.Empty;
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), errors));
    }

    private class Model1
    {
        [MayNotContain("z")]
        public string? Property { get; set; }
    }

    private class Model2
    {
        public Model2(List<string> property)
        {
            Property = property;
        }

        [MayNotContain("item")]
        public List<string> Property { get; set; }
    }

    private class Model3
    {
        [MayNotContain("z", "b")]
        public string? Property { get; set; }
    }
}
