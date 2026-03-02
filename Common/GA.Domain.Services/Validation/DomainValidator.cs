namespace GA.Domain.Services.Validation;

using System.Reflection;
using Core.Design.Attributes;

/// <summary>
///     Service for validating domain invariants.
/// </summary>
public class DomainValidator
{
    /// <summary>
    ///     Validates all annotated invariants for an object instance.
    /// </summary>
    /// <param name="instance">The object to validate.</param>
    /// <returns>A composite result of all validation checks.</returns>
    public CompositeInvariantValidationResult Validate(object instance)
    {
        var result = new CompositeInvariantValidationResult();
        if (instance == null) return result;

        var type = instance.GetType();
        
        // 1. Type-level invariants
        var typeInvariants = type.GetCustomAttributes<DomainInvariantAttribute>();
        foreach (var inv in typeInvariants)
        {
            // For now, we manually handle some common expressions or just report them
            // In a real spike, we might use an expression evaluator or specialized validators
            result.Add(new(true, $"[Type] {inv.Description}", InvariantSeverity.Info));
        }

        // 2. Property-level invariants
        var properties = type.GetProperties();
        foreach (var prop in properties)
        {
            var propInvariants = prop.GetCustomAttributes<DomainInvariantAttribute>();
            foreach (var inv in propInvariants)
            {
                var value = prop.GetValue(instance);
                var isValid = ValidateProperty(value, inv);
                result.Add(new(isValid, $"[Property {prop.Name}] {inv.Description}", isValid ? InvariantSeverity.Info : InvariantSeverity.Error));
            }
        }

        return result;
    }

    private bool ValidateProperty(object? value, DomainInvariantAttribute invariant)
    {
        // Placeholder for expression evaluation
        // If expression is empty, we assume manual validation or just metadata
        if (string.IsNullOrEmpty(invariant.Expression)) return true;

        // Example: Simple null check or range check stubs
        if (invariant.Expression == "!= null" && value == null) return false;
        
        return true; 
    }
}

