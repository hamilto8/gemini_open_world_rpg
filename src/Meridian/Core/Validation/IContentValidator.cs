using System.Collections.Generic;

namespace Meridian.Core.Validation;

/// <summary>
/// Interface for the Content Validation tool.
/// Walks registries, indexes, and directory assets to ensure integrity.
/// Enforces Section 19.10 requirements.
/// </summary>
public interface IContentValidator
{
    /// <summary>
    /// Executes validation checks on the database and asset files.
    /// Returns true if all checks pass, false if any errors are detected.
    /// </summary>
    bool ValidateContent(out List<string> errors);
}
