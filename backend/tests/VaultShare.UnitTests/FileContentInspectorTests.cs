using VaultShare.Infrastructure.Uploads;

namespace VaultShare.UnitTests;

public sealed class FileContentInspectorTests
{
    private readonly FileContentInspector _inspector = new();

    [Theory]
    [InlineData("photo.png", "89504E470D0A1A0A00000000", "image/png")]
    [InlineData("photo.jpg", "FFD8FFE00000", "image/jpeg")]
    [InlineData("document.pdf", "255044462D312E370A", "application/pdf")]
    [InlineData("image.webp", "524946460400000057454250", "image/webp")]
    public async Task Magic_bytes_determine_supported_content(string filename, string hexadecimal, string mime)
    {
        var bytes = Convert.FromHexString(hexadecimal);
        var result = await _inspector.InspectAsync(filename, new MemoryStream(bytes), default);
        Assert.True(result.IsAllowed);
        Assert.Equal(mime, result.DetectedMimeType);
        Assert.Equal(64, result.Sha256Hash.Length);
    }

    [Fact]
    public async Task Spoofed_extension_and_null_text_are_rejected()
    {
        var spoofed = await _inspector.InspectAsync("payload.png", new MemoryStream("not a png"u8.ToArray()), default);
        var binaryText = await _inspector.InspectAsync("payload.txt", new MemoryStream([0x41, 0x00, 0x42]), default);
        Assert.False(spoofed.IsAllowed);
        Assert.False(binaryText.IsAllowed);
    }
}
