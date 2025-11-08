namespace GA.Business.Core.Invariants;

/// <summary>
///     Configuration-driven invariant that can be defined in YAML
/// </summary>
public class ConfigurableInvariant<T>(InvariantDefinition definition, ICustomRuleEngine customRuleEngine)
    : InvariantBase<T>
{
    public override string InvariantName => definition.Name;
    public override string Description => definition.Description;
    public override InvariantSeverity Severity => ParseSeverity(definition.Severity);
    public override string Category => definition.Category;
    public override bool SupportsFastValidation => definition.RuleType != "Custom";

    public override TimeSpan EstimatedExecutionTime => definition.RuleType switch
    {
        "Custom" => TimeSpan.FromMilliseconds(50),
        "Regex" => TimeSpan.FromMilliseconds(20),
        "Collection" => TimeSpan.FromMilliseconds(15),
        _ => TimeSpan.FromMilliseconds(5)
    };

    public override InvariantValidationResult Validate(T obj)
    {
        try
        {
            var propertyValue = GetPropertyValue(obj, definition.TargetProperty);

            // Handle optional properties
            if (definition.Optional && propertyValue == null)
            {
                return Success();
            }

            var result = definition.RuleType switch
            {
                "NotEmpty" => ValidateNotEmpty(propertyValue),
                "Regex" => ValidateRegex(propertyValue),
                "Enum" => ValidateEnum(propertyValue),
                "String" => ValidateString(propertyValue),
                "Collection" => ValidateCollection(propertyValue),
                "Range" => ValidateRange(propertyValue),
                "Custom" => ValidateCustom(obj, propertyValue),
                _ => Failure($"Unknown rule type: {definition.RuleType}")
            };

            return result;
        }
        catch (Exception ex)
        {
            return Failure($"Error validating invariant: {ex.Message}", definition.TargetProperty);
        }
    }

    private InvariantValidationResult ValidateNotEmpty(object? value)
    {
        if (value == null || (value is string str && string.IsNullOrWhiteSpace(str)))
        {
            return Failure(definition.ErrorMessage, definition.TargetProperty, value);
        }

        return Success();
    }

    private InvariantValidationResult ValidateRegex(object? value)
    {
        if (value is not string stringValue)
        {
            return Failure("Regex validation requires a string value", definition.TargetProperty, value);
        }

        if (string.IsNullOrEmpty(definition.Pattern))
        {
            return Failure("Regex pattern not specified", definition.TargetProperty, value);
        }

        try
        {
            var regex = new Regex(definition.Pattern, RegexOptions.IgnoreCase);
            if (!regex.IsMatch(stringValue))
            {
                return Failure(definition.ErrorMessage, definition.TargetProperty, value);
            }

            return Success();
        }
        catch (Exception ex)
        {
            return Failure($"Invalid regex pattern: {ex.Message}", definition.TargetProperty, value);
        }
    }

    private InvariantValidationResult ValidateEnum(object? value)
    {
        if (value is not string stringValue)
        {
            return Failure("Enum validation requires a string value", definition.TargetProperty, value);
        }

        if (definition.AllowedValues == null || !definition.AllowedValues.Any())
        {
            return Failure("Allowed values not specified for enum validation", definition.TargetProperty, value);
        }

        if (!definition.AllowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
        {
            return Failure(definition.ErrorMessage, definition.TargetProperty, value);
        }

        return Success();
    }

    private InvariantValidationResult ValidateString(object? value)
    {
        if (value is not string stringValue)
        {
            return Failure("String validation requires a string value", definition.TargetProperty, value);
        }

        if (definition.StringRules == null)
        {
            return Success();
        }

        foreach (var rule in definition.StringRules)
        {
            var result = rule.Type switch
            {
                "MinLength" => stringValue.Length >= rule.Value ? null : rule.ErrorMessage,
                "MaxLength" => stringValue.Length <= rule.Value ? null : rule.ErrorMessage,
                "NotContains" => rule.Values?.Any(v => stringValue.Contains(v, StringComparison.OrdinalIgnoreCase)) ==
                                 true
                    ? rule.ErrorMessage
                    : null,
                _ => $"Unknown string rule type: {rule.Type}"
            };

            if (result != null)
            {
                return Failure(result, definition.TargetProperty, value);
            }
        }

        return Success();
    }

    private InvariantValidationResult ValidateCollection(object? value)
    {
        if (definition.CollectionRules == null)
        {
            return Success();
        }

        var collection = value switch
        {
            IEnumerable enumerable => enumerable.Cast<object>().ToList(),
            _ => null
        };

        if (collection == null && !definition.Optional)
        {
            return Failure("Collection validation requires an enumerable value", definition.TargetProperty, value);
        }

        if (collection == null)
        {
            return Success(); // Optional collection
        }

        foreach (var rule in definition.CollectionRules)
        {
            var result = rule.Type switch
            {
                "NotEmpty" => collection.Any() ? null : rule.ErrorMessage,
                "MinCount" => collection.Count >= rule.Value ? null : rule.ErrorMessage,
                "MaxCount" => collection.Count <= rule.Value ? null : rule.ErrorMessage,
                "ExactCount" => collection.Count == rule.Value ? null : rule.ErrorMessage,
                "Range" => ValidateCollectionRange(collection, rule),
                "Unique" => collection.Count == collection.Distinct().Count() ? null : rule.ErrorMessage,
                "Enum" => ValidateCollectionEnum(collection, rule),
                "Custom" => ValidateCollectionCustom(collection, rule),
                _ => $"Unknown collection rule type: {rule.Type}"
            };

            if (result != null)
            {
                return Failure(result, definition.TargetProperty, value);
            }
        }

        return Success();
    }

    private string? ValidateCollectionRange(List<object> collection, CollectionRule rule)
    {
        foreach (var item in collection)
        {
            if (item is int intValue)
            {
                if (intValue < rule.Min || intValue > rule.Max)
                {
                    return rule.ErrorMessage;
                }
            }
            else if (item is double doubleValue)
            {
                if (doubleValue < rule.Min || doubleValue > rule.Max)
                {
                    return rule.ErrorMessage;
                }
            }
        }

        return null;
    }

    private string? ValidateCollectionEnum(List<object> collection, CollectionRule rule)
    {
        if (rule.AllowedValues == null)
        {
            return "Allowed values not specified for collection enum validation";
        }

        foreach (var item in collection)
        {
            if (item is string stringItem && !rule.AllowedValues.Contains(stringItem, StringComparer.OrdinalIgnoreCase))
            {
                return rule.ErrorMessage;
            }
        }

        return null;
    }

    private string? ValidateCollectionCustom(List<object> collection, CollectionRule rule)
    {
        return rule.Rule switch
        {
            "AtLeastPlayedStrings" => ValidateAtLeastPlayedStrings(collection, rule),
            _ => $"Unknown custom collection rule: {rule.Rule}"
        };
    }

    private string? ValidateAtLeastPlayedStrings(List<object> collection, CollectionRule rule)
    {
        var minPlayedObj = rule.Parameters?.GetValueOrDefault("min_played", 2) ?? 2;
        var minPlayed = Convert.ToInt32(minPlayedObj);
        var playedStrings = collection.Count(item => item is int intValue && intValue >= 0);

        return playedStrings >= minPlayed ? null : rule.ErrorMessage;
    }

    private InvariantValidationResult ValidateRange(object? value)
    {
        if (value is int intValue)
        {
            if (intValue < definition.Min || intValue > definition.Max)
            {
                return Failure(definition.ErrorMessage, definition.TargetProperty, value);
            }
        }
        else if (value is double doubleValue)
        {
            if (doubleValue < definition.Min || doubleValue > definition.Max)
            {
                return Failure(definition.ErrorMessage, definition.TargetProperty, value);
            }
        }
        else
        {
            return Failure("Range validation requires a numeric value", definition.TargetProperty, value);
        }

        return Success();
    }

    private InvariantValidationResult ValidateCustom(T obj, object? value)
    {
        return customRuleEngine.ValidateCustomRule(definition.Rule, obj, value, definition.Parameters);
    }

    private static object? GetPropertyValue(T obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }

        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        return property?.GetValue(obj);
    }

    private static InvariantSeverity ParseSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "info" => InvariantSeverity.Info,
            "warning" => InvariantSeverity.Warning,
            "error" => InvariantSeverity.Error,
            "critical" => InvariantSeverity.Critical,
            _ => InvariantSeverity.Error
        };
    }
}

/// <summary>
///     Configuration model for invariant definitions
/// </summary>
public class InvariantDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
    public string RuleType { get; set; } = string.Empty;
    public string TargetProperty { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool Optional { get; set; }

    // Rule-specific properties
    public string? Pattern { get; set; }
    public List<string>? AllowedValues { get; set; }
    public List<StringRule>? StringRules { get; set; }
    public List<CollectionRule>? CollectionRules { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public string? Rule { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

public class StringRule
{
    public string Type { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<string>? Values { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class CollectionRule
{
    public string Type { get; set; } = string.Empty;
    public int Value { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? Rule { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public class InvariantGroupDefinition
{
    public string TargetType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<InvariantDefinition> Invariants { get; set; } = [];
}

public class InvariantConfiguration
{
    public Dictionary<string, InvariantGroupDefinition> InvariantGroups { get; set; } = [];
    public InvariantSettings Settings { get; set; } = new();
    public Dictionary<string, InvariantSettings> Environments { get; set; } = [];
}

public class InvariantSettings
{
    public bool CacheEnabled { get; set; } = true;
    public int CacheDurationMinutes { get; set; } = 15;
    public bool PerformanceMonitoring { get; set; } = true;
    public bool AsyncValidation { get; set; } = true;
    public int MaxConcurrentValidations { get; set; } = 8;
    public int ValidationTimeoutSeconds { get; set; } = 30;
    public List<string>? SeverityFilter { get; set; }
}

/// <summary>
///     Interface for custom rule validation
/// </summary>
public interface ICustomRuleEngine
{
    InvariantValidationResult ValidateCustomRule(string? ruleName, object obj, object? value,
        Dictionary<string, object>? parameters);
}

/// <summary>
///     Default implementation of custom rule engine
/// </summary>
public class DefaultCustomRuleEngine : ICustomRuleEngine
{
    public InvariantValidationResult ValidateCustomRule(string? ruleName, object obj, object? value,
        Dictionary<string, object>? parameters)
    {
        // Implement custom rules here
        return ruleName switch
        {
            "MusicTheoryConsistency" => ValidateMusicTheoryConsistency(obj, value, parameters),
            "GuitarPlayability" => ValidateGuitarPlayability(obj, value, parameters),
            _ => new InvariantValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Unknown custom rule: {ruleName}"
            }
        };
    }

    private InvariantValidationResult ValidateMusicTheoryConsistency(object obj, object? value,
        Dictionary<string, object>? parameters)
    {
        // Implement music theory consistency validation
        return new InvariantValidationResult { IsValid = true };
    }

    private InvariantValidationResult ValidateGuitarPlayability(object obj, object? value,
        Dictionary<string, object>? parameters)
    {
        // Implement guitar playability validation
        return new InvariantValidationResult { IsValid = true };
    }
}
