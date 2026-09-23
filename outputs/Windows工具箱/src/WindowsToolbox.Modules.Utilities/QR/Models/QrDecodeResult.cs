namespace WindowsToolbox.Modules.Utilities.QR.Models;

public enum QrContentKind
{
    Text,
    HttpUrl,
    HttpsUrl
}

public sealed record QrDecodeResult(string Text, QrContentKind Kind)
{
    public int CharacterCount => Text.Length;

    public static QrDecodeResult FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return new QrDecodeResult(text, QrContentKind.HttpUrl);
        if (Uri.TryCreate(text, UriKind.Absolute, out uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return new QrDecodeResult(text, QrContentKind.HttpsUrl);
        return new QrDecodeResult(text, QrContentKind.Text);
    }
}
