using System;
using System.ComponentModel.DataAnnotations;
using SharedBase.Utilities;

namespace SharedBase.ModelVerifiers;

/// <summary>
///   Basic validation that (if not null) that a string looks like an email
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class EmailAttribute : RequiredAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Allow null values
        if (ReferenceEquals(value, null))
            return ValidationResult.Success;

        var asString = value as string;

        if (asString == null)
        {
            throw new InvalidOperationException(
                $"Can't apply {nameof(EmailAttribute)} to a non-string type");
        }

        var atIndex = asString.IndexOf('@');

        if (atIndex < 1 || atIndex + 1 >= asString.Length)
        {
            return new ValidationResult(
                ErrorMessage ??
                $"The {validationContext.DisplayName} field must be an email address" +
                "(missing @ character at valid position).",
                new[] { validationContext.MemberName! });
        }

        if (asString.Length > GlobalConstants.MaxEmailLength || asString.Length < GlobalConstants.MinEmailLength)
        {
            return new ValidationResult(
                ErrorMessage ??
                $"The {validationContext.DisplayName} field must be an email address" +
                "(address is too long or too short).",
                new[] { validationContext.MemberName! });
        }

        if (!asString.Trim().Equals(asString))
        {
            return new ValidationResult(
                ErrorMessage ??
                $"The {validationContext.DisplayName} field must be an email address" +
                "(there is trailing or preceding whitespace).",
                new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
