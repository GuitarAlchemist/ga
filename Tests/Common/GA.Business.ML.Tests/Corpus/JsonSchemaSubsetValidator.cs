namespace GA.Business.ML.Tests.Corpus;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
///     A deliberately small JSON Schema validator covering exactly the keywords
///     used by <c>progression-corpus.v1.schema.json</c>.
/// </summary>
/// <remarks>
///     <para>
///         Why hand-rolled rather than a NuGet validator: the corpus lives in a
///         test project whose <c>.csproj</c> is a one-way-door path under
///         <c>agent-blackbox.policy.json</c>. A ~200-line closed-world validator
///         over a known keyword set is cheaper than a dependency change, and it
///         keeps the schema load-bearing instead of decorative - the schema is
///         the language-neutral contract other repos read, so it must actually
///         be enforced against the data it describes.
///     </para>
///     <para>
///         The closed world is the safety property. <see cref="SupportedKeywords" />
///         is asserted against the schema document by
///         <c>ProgressionCorpusLoaderTests</c>; if anyone adds <c>oneOf</c>,
///         <c>if/then</c>, <c>$dynamicRef</c> or any other keyword this file does
///         not implement, that test fails rather than the validator silently
///         ignoring a constraint and reporting a false pass.
///     </para>
/// </remarks>
internal static class JsonSchemaSubsetValidator
{
    /// <summary>
    ///     Every keyword this validator understands. Annotation-only keywords
    ///     (<c>$schema</c>, <c>$id</c>, <c>title</c>, <c>description</c>,
    ///     <c>$defs</c>) are listed because they may legally appear, not because
    ///     they constrain anything.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedKeywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "$schema", "$id", "$ref", "$defs", "title", "description",
        "type", "const", "enum", "required", "properties", "additionalProperties",
        "items", "minItems", "maxItems", "uniqueItems",
        "minimum", "maximum", "minLength", "pattern"
    };

    /// <summary>Validates <paramref name="instance" /> against <paramref name="schema" />.</summary>
    /// <returns>One message per violation, JSON-pointer prefixed. Empty means valid.</returns>
    public static IReadOnlyList<string> Validate(JsonElement instance, JsonElement schema)
    {
        var errors = new List<string>();
        Walk(instance, schema, schema, "#", errors);
        return errors;
    }

    /// <summary>
    ///     Collects every keyword appearing anywhere in a schema document, so a
    ///     test can assert the document stays inside <see cref="SupportedKeywords" />.
    /// </summary>
    public static IReadOnlySet<string> CollectKeywords(JsonElement schema)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        Collect(schema);
        return found;

        // Only descends where a schema legally nests another schema. Keys under
        // `properties` / `$defs` are author-chosen names, never keywords.
        void Collect(JsonElement node)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            foreach (var prop in node.EnumerateObject())
            {
                found.Add(prop.Name);
                switch (prop.Name)
                {
                    case "properties":
                    case "$defs":
                        foreach (var sub in prop.Value.EnumerateObject()) Collect(sub.Value);
                        break;
                    case "items":
                        Collect(prop.Value);
                        break;
                    case "additionalProperties":
                        if (prop.Value.ValueKind == JsonValueKind.Object) Collect(prop.Value);
                        break;
                }
            }
        }
    }

    private static void Walk(JsonElement instance, JsonElement schema, JsonElement root, string path, List<string> errors)
    {
        if (schema.TryGetProperty("$ref", out var refNode))
        {
            var resolved = Resolve(root, refNode.GetString()!);
            Walk(instance, resolved, root, path, errors);
            return;
        }

        if (schema.TryGetProperty("type", out var typeNode) && !MatchesType(instance, typeNode))
            errors.Add($"{path}: expected type {Describe(typeNode)}, found {instance.ValueKind}");

        if (schema.TryGetProperty("const", out var constNode) && !JsonEquals(instance, constNode))
            errors.Add($"{path}: expected const {constNode.GetRawText()}, found {instance.GetRawText()}");

        if (schema.TryGetProperty("enum", out var enumNode) &&
            !enumNode.EnumerateArray().Any(allowed => JsonEquals(instance, allowed)))
            errors.Add($"{path}: {instance.GetRawText()} is not one of {enumNode.GetRawText()}");

        switch (instance.ValueKind)
        {
            case JsonValueKind.Object:
                ValidateObject(instance, schema, root, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(instance, schema, root, path, errors);
                break;
            case JsonValueKind.String:
                ValidateString(instance, schema, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(instance, schema, path, errors);
                break;
        }
    }

    private static void ValidateObject(JsonElement instance, JsonElement schema, JsonElement root, string path, List<string> errors)
    {
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var name in required.EnumerateArray().Select(e => e.GetString()!))
                if (!instance.TryGetProperty(name, out _))
                    errors.Add($"{path}: missing required property '{name}'");
        }

        var hasProperties = schema.TryGetProperty("properties", out var properties);

        if (schema.TryGetProperty("additionalProperties", out var extra) &&
            extra.ValueKind == JsonValueKind.False)
        {
            foreach (var prop in instance.EnumerateObject())
                if (!hasProperties || !properties.TryGetProperty(prop.Name, out _))
                    errors.Add($"{path}: unexpected property '{prop.Name}'");
        }

        if (!hasProperties) return;

        foreach (var prop in instance.EnumerateObject())
            if (properties.TryGetProperty(prop.Name, out var propSchema))
                Walk(prop.Value, propSchema, root, $"{path}/{prop.Name}", errors);
    }

    private static void ValidateArray(JsonElement instance, JsonElement schema, JsonElement root, string path, List<string> errors)
    {
        var length = instance.GetArrayLength();

        if (schema.TryGetProperty("minItems", out var min) && length < min.GetInt32())
            errors.Add($"{path}: expected at least {min.GetInt32()} items, found {length}");

        if (schema.TryGetProperty("maxItems", out var max) && length > max.GetInt32())
            errors.Add($"{path}: expected at most {max.GetInt32()} items, found {length}");

        if (schema.TryGetProperty("uniqueItems", out var unique) && unique.ValueKind == JsonValueKind.True)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in instance.EnumerateArray())
                if (!seen.Add(Canonical(item)))
                    errors.Add($"{path}: duplicate item {item.GetRawText()}");
        }

        if (!schema.TryGetProperty("items", out var itemSchema)) return;

        var index = 0;
        foreach (var item in instance.EnumerateArray())
            Walk(item, itemSchema, root, $"{path}/{index++}", errors);
    }

    private static void ValidateString(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var value = instance.GetString() ?? string.Empty;

        if (schema.TryGetProperty("minLength", out var minLength) && value.Length < minLength.GetInt32())
            errors.Add($"{path}: expected minLength {minLength.GetInt32()}, found {value.Length}");

        if (schema.TryGetProperty("pattern", out var pattern) &&
            !Regex.IsMatch(value, pattern.GetString()!, RegexOptions.None, TimeSpan.FromSeconds(2)))
            errors.Add($"{path}: '{value}' does not match pattern {pattern.GetString()}");
    }

    private static void ValidateNumber(JsonElement instance, JsonElement schema, string path, List<string> errors)
    {
        var value = instance.GetDouble();

        if (schema.TryGetProperty("minimum", out var min) && value < min.GetDouble())
            errors.Add($"{path}: {value} is below minimum {min.GetDouble()}");

        if (schema.TryGetProperty("maximum", out var max) && value > max.GetDouble())
            errors.Add($"{path}: {value} is above maximum {max.GetDouble()}");
    }

    private static JsonElement Resolve(JsonElement root, string pointer)
    {
        if (!pointer.StartsWith("#/", StringComparison.Ordinal))
            throw new NotSupportedException($"only local '#/...' refs are supported, got '{pointer}'");

        var node = root;
        foreach (var segment in pointer[2..].Split('/'))
        {
            if (!node.TryGetProperty(segment, out node))
                throw new InvalidOperationException($"unresolvable $ref segment '{segment}' in '{pointer}'");
        }

        return node;
    }

    private static bool MatchesType(JsonElement instance, JsonElement typeNode) =>
        typeNode.ValueKind == JsonValueKind.Array
            ? typeNode.EnumerateArray().Any(t => MatchesTypeName(instance, t.GetString()!))
            : MatchesTypeName(instance, typeNode.GetString()!);

    private static bool MatchesTypeName(JsonElement instance, string typeName) => typeName switch
    {
        "object"  => instance.ValueKind == JsonValueKind.Object,
        "array"   => instance.ValueKind == JsonValueKind.Array,
        "string"  => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null"    => instance.ValueKind == JsonValueKind.Null,
        "number"  => instance.ValueKind == JsonValueKind.Number,
        "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
        _         => throw new NotSupportedException($"unsupported type name '{typeName}'")
    };

    private static string Describe(JsonElement typeNode) =>
        typeNode.ValueKind == JsonValueKind.Array
            ? string.Join("|", typeNode.EnumerateArray().Select(t => t.GetString()))
            : typeNode.GetString()!;

    private static bool JsonEquals(JsonElement left, JsonElement right) =>
        string.Equals(Canonical(left), Canonical(right), StringComparison.Ordinal);

    /// <summary>Order-insensitive-for-objects textual form, used for equality and uniqueness.</summary>
    private static string Canonical(JsonElement element)
    {
        var sb = new StringBuilder();
        Append(element);
        return sb.ToString();

        void Append(JsonElement node)
        {
            switch (node.ValueKind)
            {
                case JsonValueKind.Object:
                    sb.Append('{');
                    foreach (var prop in node.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                    {
                        sb.Append(JsonSerializer.Serialize(prop.Name)).Append(':');
                        Append(prop.Value);
                        sb.Append(',');
                    }

                    sb.Append('}');
                    break;
                case JsonValueKind.Array:
                    sb.Append('[');
                    foreach (var item in node.EnumerateArray())
                    {
                        Append(item);
                        sb.Append(',');
                    }

                    sb.Append(']');
                    break;
                case JsonValueKind.Number:
                    sb.Append(node.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(node.GetRawText());
                    break;
            }
        }
    }
}
