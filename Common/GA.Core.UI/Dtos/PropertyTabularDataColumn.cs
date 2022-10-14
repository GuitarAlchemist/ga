namespace GA.Core.UI.Dtos;

using System.Reflection;

public record PropertyTabularDataColumn(PropertyInfo Property) : TabularDataColumn(Property.Name, Property.PropertyType.Name);