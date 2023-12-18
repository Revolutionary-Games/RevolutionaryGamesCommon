namespace SharedBase.ModelVerifiers;

using System;
using System.ComponentModel.DataAnnotations;
using Utilities;

/// <summary>
///   Validates that a property when converted to string is not too long. This exists because
///   <see cref="MaxLengthAttribute"/> cannot be used on <see cref="Uri"/> and some other important types.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ToStringMaxLengthAttribute : RequiredAttribute
{
    public ToStringMaxLengthAttribute(int maxLength = GlobalConstants.DEFAULT_MAX_LENGTH_FOR_TO_STRING_ATTRIBUTE)
    {
        MaxLength = maxLength;
    }

    public int MaxLength { get; set; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Allow null values, separate RequiredAttribute can be added
        if (ReferenceEquals(value, null))
            return ValidationResult.Success;

        var asString = value.ToString();

        if (asString == null)
        {
            throw new InvalidOperationException(
                $"Can't apply {nameof(ToStringMaxLengthAttribute)} to an object that returns null from ToString call");
        }

        if (asString.Length > MaxLength)
        {
            return new ValidationResult(ErrorMessage ??
                $"The {validationContext.DisplayName} field is too long (length: {asString.Length}). " +
                $"It may not be longer than {MaxLength} characters.",
                new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
