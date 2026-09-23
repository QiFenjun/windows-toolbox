namespace WindowsToolbox.Modules.Utilities.Random.Models;

public sealed record RandomStringOptions(
    bool Uppercase = true,
    bool Lowercase = true,
    bool Digits = true,
    bool Symbols = false);
