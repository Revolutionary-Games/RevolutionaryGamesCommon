namespace SharedBase.ModelVerifiers;

using System;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

/// <summary>
///   Requires that a property does not contain whitespace (or is not a list containing something with whitespace)
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class NoWhitespaceAttribute : RequiredAttribute
{
    private static readonly Regex WhitespaceRegex = new(@"\s");

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (ReferenceEquals(value, null))
            return ValidationResult.Success;

        if (value is string valueString)
        {
            if (WhitespaceRegex.IsMatch(valueString))
            {
                return new ValidationResult(ErrorMessage ??
                    $"The {validationContext.DisplayName} field may not contain whitespace.",
                    new[] { validationContext.MemberName! });
            }
        }
        else if (value is IEnumerable valueEnumerable)
        {
            bool found = false;
            foreach (var existingValue in valueEnumerable)
            {
                if (existingValue is string asString)
                {
                    if (WhitespaceRegex.IsMatch(asString))
                    {
                        found = true;
                        break;
                    }

                    continue;
                }

                if (WhitespaceRegex.IsMatch(existingValue.ToString() ??
                        throw new InvalidOperationException("Value in an enumerable can't be converted to a string")))
                {
                    found = true;
                    break;
                }
            }

            if (found)
            {
                return new ValidationResult(ErrorMessage ??
                    $"The {validationContext.DisplayName} field may not contain whitespace.",
                    new[] { validationContext.MemberName! });
            }
        }
        else
        {
            return new ValidationResult(ErrorMessage ??
                $"The {validationContext.DisplayName} field is of unknown type to check that it does not " +
                "contain whitespace.", new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success;
    }
}
