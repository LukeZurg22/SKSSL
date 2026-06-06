namespace SKSSL;

/// <summary>
/// A toggle for what type of files the game is expected to read from dedicated prototypes folders.
/// This does not prevent custom handling.
/// </summary>
public enum ContentTypeToggle : byte
{
    /// The game will not handle prototypes folders whatsoever. Instead, you are obligated to create -your own- loader.
    CUSTOM = 0,

    /// Load content prototype files as YAML. More legible.
    YAML,

    /// Load content prototype files as JSON. More memory-efficient.
    JSON,
}