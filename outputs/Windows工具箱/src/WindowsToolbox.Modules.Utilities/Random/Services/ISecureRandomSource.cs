namespace WindowsToolbox.Modules.Utilities.Random.Services;

public interface ISecureRandomSource
{
    int GetInt32(int maxExclusive);
    void Fill(Span<byte> buffer);
}
