namespace SharedBase.ModelVerifiers;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
///   Validates that property is a valid <see cref="Uri"/>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class IsUriAttribute : RequiredAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Allow null values and also immediately allow if the object is already a Uri
        if (value is null or Uri)
            return ValidationResult.Success;

        if (value is not string asString)
        {
            throw new InvalidOperationException($"Can't apply {nameof(IsUriAttribute)} to a non-string type");
        }

        // We allow blanks as Required attribute disallows empty strings
        if (asString.Length < 1)
            return ValidationResult.Success;

        if (!Uri.TryCreate(asString, UriKind.Absolute, out _))
        {
            return new ValidationResult(ErrorMessage ??
                $"The {validationContext.DisplayName} field must be a valid URL.",
                new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
