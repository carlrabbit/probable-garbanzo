using System;

namespace Analyzers;

/// <summary>
/// Specifies a default value for a TUnit <c>Property</c> on test methods in the annotated class.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class TUnitDefaultPropertyAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TUnitDefaultPropertyAttribute"/> class.
    /// </summary>
    /// <param name="property">The property name.</param>
    /// <param name="value">The default property value.</param>
    public TUnitDefaultPropertyAttribute(string property, string value)
    {
        Property = property;
        Value = value;
    }

    /// <summary>The property name.</summary>
    public string Property { get; }

    /// <summary>The default property value.</summary>
    public string Value { get; }
}
