namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents configuration for the Clock standard-library module.</summary>
public sealed class ClockStandardLibraryOptions
{
    /// <summary>Gets or sets the time provider.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
