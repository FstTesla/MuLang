namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents configuration for the Guid standard-library module.</summary>
public sealed class GuidStandardLibraryOptions
{
    /// <summary>Gets or sets the time provider.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
