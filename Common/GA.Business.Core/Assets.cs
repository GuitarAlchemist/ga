namespace GA.Business.Core;

using Tonal;
using Notes;

[PublicAPI]
public static class Assets
{
    public static IReadOnlyCollection<Note.Flat> FlatNotes => Note.Flat.Items;
    public static IReadOnlyCollection<Note.Sharp> SharpNotes => Note.Sharp.Items;
    public static IReadOnlyCollection<Key> Keys => Key.Items;

    private static readonly Lazy<ImmutableDictionary<string, Asset>> _indexedProperties = new(CreateAssetByName, true);
   
    /// <summary>
    /// Gets the asset names
    /// </summary>
    public static IEnumerable<string> Names => _indexedProperties.Value.Keys;

    /// <summary>
    /// Gets an asset by name
    /// </summary>
    /// <param name="name">The asset name <see cref="string"/></param>
    /// <returns>The <see cref="Asset"/></returns>
    /// <exception cref="KeyNotFoundException">Thrown when the asset is not found</exception>
    public static Asset Get(string name)
    {
        if (!_indexedProperties.Value.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Property {name} not found in Assets.");
        }
        
        return value;
    }
    
    private static ImmutableDictionary<string, Asset> CreateAssetByName()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, Asset>();
        var properties = typeof(Assets).GetProperties(BindingFlags.Static | BindingFlags.Public);
        foreach (var property in properties)
        {
            if (!typeof(IReadOnlyCollection<>).IsAssignableFrom(property.PropertyType.GetGenericTypeDefinition())) continue;
            var name = property.Name;
            var value = (IReadOnlyCollection<object>)property.GetValue(null)!;
            var asset = new Asset(name, value);
            builder.Add(property.Name, asset);
        }

        return builder.ToImmutable();
    }
}

public record Asset(string Name, IReadOnlyCollection<object> Items);