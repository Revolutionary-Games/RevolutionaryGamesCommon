namespace SharedBase.ModelVerifiers;

using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

/// <summary>
///   Requires that a property is null or does not contain any of the specified items
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class MayNotContainAttribute : RequiredAttribute
{
    public MayNotContainAttribute(params string[] values)
    {
        Values = values;
    }

    public string[] Values { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (ReferenceEquals(value, null))
            return ValidationResult.Success;

        foreach (var valueToCheck in Values)
        {
            if (string.IsNullOrEmpty(valueToCheck))
            {
                throw new InvalidOperationException(
                    $"{nameof(MayNotContainAttribute)} is configured wrong with an empty value to check");
            }

            if (value is string valueString)
            {
                if (valueString.Contains(valueToCheck))
                {
                    return new ValidationResult(
                        ErrorMessage ??
                        $"The {validationContext.DisplayName} field may not contain '{valueToCheck}'.",
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
                        if (valueToCheck.Equals(asString))
                        {
                            found = true;
                            break;
                        }

                        continue;
                    }

                    var converter = TypeDescriptor.GetConverter(existingValue.GetType());
                    if (valueToCheck.Equals(converter.ConvertFromInvariantString(existingValue.ToString() ??
                            throw new InvalidOperationException(
                                "Value in an enumerable can't be converted to a string"))))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    return new ValidationResult(
                        ErrorMessage ??
                        $"The {validationContext.DisplayName} field may not contain '{valueToCheck}'.",
                        new[] { validationContext.MemberName! });
                }
            }
            else
            {
                return new ValidationResult(
                    ErrorMessage ??
                    $"The {validationContext.DisplayName} field is of unknown type to check that it does not " +
                    "contain a disallowed value.", new[] { validationContext.MemberName! });
            }
        }

        return ValidationResult.Success;
    }
}
