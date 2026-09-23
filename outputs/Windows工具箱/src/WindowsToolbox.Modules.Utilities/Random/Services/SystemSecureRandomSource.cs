using System.Security.Cryptography;

namespace WindowsToolbox.Modules.Utilities.Random.Services;

public sealed class SystemSecureRandomSource : ISecureRandomSource
{
    public int GetInt32(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
    public void Fill(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);
}
