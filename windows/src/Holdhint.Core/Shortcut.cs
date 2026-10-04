namespace Holdhint.Core;

public sealed record Shortcut(ModifierSet Modifiers, string Key, string Title, string? Note = null);
