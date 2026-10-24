namespace TableOrder.Terminal.Modules;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Smart.Mvvm.Resolver;

public static class ValidationHelper
{
    [ThreadStatic]
    private static List<ValidationResult>? validationResults;

    public static void Validate(object target, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, string name, ErrorInfo errors)
    {
        var accessor = AccessorProvider.FindAccessor(type);
        if (accessor is null)
        {
            throw new InvalidOperationException($"Accessor is not supported. type=[{type}]");
        }

        var value = accessor.GetValue(target, name);
        var property = type.GetProperty(name);
        var context = new ValidationContext(target, property?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? name, ResolveProvider.Default, null)
        {
            MemberName = name
        };
        validationResults ??= [];

        if (!Validator.TryValidateValue(value, context, validationResults, property?.GetCustomAttributes<ValidationAttribute>() ?? []))
        {
            errors.AddError(name, validationResults[0].ErrorMessage!);
        }

        validationResults.Clear();
    }
}
