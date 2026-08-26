using Ncr7198.PiBridge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ncr7198.PiBridge.Tests;

public sealed class ReceiptRendererTests
{
    private readonly ReceiptRenderer _renderer = new();

    [Fact]
    public void Defaults_RenderOneCopyWithFourPostLinesAndCut()
    {
        var job = _renderer.Render(new PrintRequest { Content = "Hello" });

        Assert.Equal(new[] { "Hello", "", "", "", "", "[CUT]" }, job.Preview);
        Assert.True(job.EffectiveCut);
        Assert.False(job.CutForced);
        Assert.True(job.Bytes.AsSpan().IndexOf(new byte[] { 0x1D, 0x56, 0x41, 0x00 }) >= 0);
    }

    [Fact]
    public void Compressed_AllowsFiftySixColumns()
    {
        var text = new string('X', 56);
        var job = _renderer.Render(new PrintRequest { Content = text, Compressed = true });

        Assert.Equal(text, job.Preview[0]);
        Assert.Equal(new byte[] { 0x10, 0x1B, 0x16, 0x01 }, job.Bytes[..4]);
    }

    [Fact]
    public void Standard_RejectsMoreThanFortyFourColumns()
    {
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = new string('X', 45) }));

        Assert.Contains("maximum width is 44", exception.Message);
    }

    [Fact]
    public void WordWrap_IsOnlyAvailableForContent()
    {
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Lines = ["Hello"], Wrap = "word" }));

        Assert.Contains("when lines is supplied", exception.Message);
    }

    [Fact]
    public void WordWrap_UsesWhitespaceAndPreservesExplicitBlankLines()
    {
        var job = _renderer.Render(new PrintRequest
        {
            Content = $"{new string('A', 40)} word\n\nDone",
            Wrap = "word",
            PostPrintLines = 0,
            Cut = false
        });

        Assert.Equal(new[] { new string('A', 40), "word", "", "Done" }, job.Preview);
    }

    [Fact]
    public void WordWrap_SplitsLongUnbrokenContentWithoutRejectingIt()
    {
        var text = new string('X', 45);
        var job = _renderer.Render(new PrintRequest
        {
            Content = text, Wrap = "word", PostPrintLines = 0, Cut = false
        });

        Assert.Equal(new[] { new string('X', 44), "X" }, job.Preview);
    }

    [Fact]
    public void Lines_WinWhenContentIsAlsoSupplied()
    {
        var job = _renderer.Render(new PrintRequest
        {
            Lines = ["LINES"], Content = "CONTENT", PostPrintLines = 0, Cut = false
        });

        Assert.Equal(new[] { "LINES" }, job.Preview);
    }

    [Fact]
    public void UnsupportedUnicode_IsRejectedWithLocation()
    {
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = "Coffee \u2615" }));

        Assert.Contains("U+2615", exception.Message);
        Assert.Contains("character 8", exception.Message);
    }

    [Fact]
    public void Content_IsLeftAlignedWhileLinesRemainLiteral()
    {
        var content = _renderer.Render(new PrintRequest { Content = "   Hello", PostPrintLines = 0, Cut = false });
        var lines = _renderer.Render(new PrintRequest { Lines = ["   Hello"], PostPrintLines = 0, Cut = false });

        Assert.Equal("Hello", content.Preview[0]);
        Assert.Equal("   Hello", lines.Preview[0]);
    }

    [Fact]
    public void MultipleCopies_ForceCutAfterEveryCopy()
    {
        var job = _renderer.Render(new PrintRequest
        {
            Content = "Copy", PostPrintLines = 0, Copies = 2, Cut = false
        });

        Assert.Equal(new[] { "Copy", "[CUT]", "Copy", "[CUT]" }, job.Preview);
        Assert.True(job.EffectiveCut);
        Assert.True(job.CutForced);
    }

    [Fact]
    public void Logo_DefaultsToTopAndEmitsCenteredTwentyFourDotRasterData()
    {
        var logo = AsDataUrl(CreateBmp(2, 1, (x, _) => x == 0));
        var job = _renderer.Render(new PrintRequest
        {
            Content = "Text", Logo = logo, PostPrintLines = 0, Cut = false
        });

        Assert.Equal("[LOGO: 2x1]", job.Preview[0]);
        Assert.Equal("Text", job.Preview[1]);
        var command = job.Bytes.AsSpan().IndexOf(new byte[] { 0x1B, 0x2A, 0x21, 0x02, 0x00 });
        Assert.True(command >= 0);
        Assert.Equal(new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00 }, job.Bytes[(command + 5)..(command + 11)]);
        Assert.True(job.Bytes.AsSpan().IndexOf(new byte[] { 0x1B, 0x61, 0x01 }) >= 0);
        Assert.True(job.Bytes.AsSpan().IndexOf(new byte[] { 0x1B, 0x33, 0x30 }) >= 0);
        Assert.True(job.Bytes.AsSpan().IndexOf(new byte[] { 0x1B, 0x33, 0x36 }) >= 0);
    }

    [Fact]
    public void Logo_CanBePlacedBelowText()
    {
        var job = _renderer.Render(new PrintRequest
        {
            Content = "Text", Logo = Convert.ToBase64String(CreateBmp(1, 1, (_, _) => true)),
            LogoPosition = "bottom", PostPrintLines = 0, Cut = false
        });

        Assert.Equal("Text", job.Preview[0]);
        Assert.StartsWith("[LOGO:", job.Preview[1]);
    }

    [Fact]
    public void Logo_ScalesDownToReceiptWidthButDoesNotScaleUp()
    {
        var large = _renderer.Render(new PrintRequest
        {
            Content = "Text", Logo = Convert.ToBase64String(CreateBmp(600, 100, (_, _) => true))
        });
        var small = _renderer.Render(new PrintRequest
        {
            Content = "Text", Logo = Convert.ToBase64String(CreateBmp(100, 20, (_, _) => true))
        });

        Assert.Equal("[LOGO: 576x96]", large.Preview[0]);
        Assert.Equal("[LOGO: 100x20]", small.Preview[0]);
    }

    [Fact]
    public void LogoPosition_RejectsUnknownValues()
    {
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = "Text", LogoPosition = "middle" }));

        Assert.Contains("logoPosition", exception.Message);
    }

    [Fact]
    public void Logo_RejectsInvalidBase64()
    {
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = "Text", Logo = "not-base64" }));

        Assert.Contains("valid Base64", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void FeedBounds_AreEnforced(int lines)
    {
        Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = "Test", PrePrintLines = lines }));
    }

    [Fact]
    public void PaperLimit_AllowsAtMostEightEstimatedInches()
    {
        var withinLimit = string.Join('\n', Enumerable.Repeat("X", 50));
        var overLimit = string.Join('\n', Enumerable.Repeat("X", 51));

        _renderer.Render(new PrintRequest { Content = withinLimit });
        var exception = Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = overLimit }));

        Assert.Contains("8.13 inches", exception.Message);
        Assert.Contains("maximum is 8 inches", exception.Message);
    }

    [Fact]
    public void PaperEstimate_MatchesPhysicalFortyOneLineCalibrationReceipt()
    {
        var rows = 41;
        var estimatedInches = rows / ReceiptRenderer.CalibratedTextLinesPerInch +
            ReceiptRenderer.CalibratedCutterAllowanceInches;

        Assert.InRange(estimatedInches * 2.54, 15.75, 15.90);
    }

    [Fact]
    public async Task PrintId_DeduplicatesTheSameEffectiveJob()
    {
        var transport = new RecordingTransport();
        var coordinator = new PrintCoordinator(transport, new BridgeOptions(), NullLogger<PrintCoordinator>.Instance);
        await coordinator.StartAsync(CancellationToken.None);
        try
        {
            var job = _renderer.Render(new PrintRequest { PrintId = "order-1", Content = "Test" });
            var first = coordinator.Submit(job);
            await first.Result;
            var duplicate = coordinator.Submit(job);
            await duplicate.Result;

            Assert.False(first.IsDuplicate);
            Assert.True(duplicate.IsDuplicate);
            Assert.Equal(1, transport.WriteCount);
        }
        finally { await coordinator.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task PrintId_RejectsDifferentEffectiveJob()
    {
        var coordinator = new PrintCoordinator(new RecordingTransport(), new BridgeOptions(), NullLogger<PrintCoordinator>.Instance);
        await coordinator.StartAsync(CancellationToken.None);
        try
        {
            var first = _renderer.Render(new PrintRequest { PrintId = "order-1", Content = "First" });
            await coordinator.Submit(first).Result;
            var changed = _renderer.Render(new PrintRequest { PrintId = "order-1", Content = "Changed" });

            Assert.Throws<PrintIdConflictException>(() => coordinator.Submit(changed));
        }
        finally { await coordinator.StopAsync(CancellationToken.None); }
    }

    private sealed class RecordingTransport : IPrinterTransport
    {
        public string Mode => "Device";
        public string Description => "test";
        public int WriteCount { get; private set; }
        public bool IsAvailable() => true;
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    private static byte[] CreateBmp(int width, int height, Func<int, int, bool> isBlack)
    {
        var rowBytes = ((width * 3 + 3) / 4) * 4;
        var pixelBytes = rowBytes * height;
        var bytes = new byte[54 + pixelBytes];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(pixelBytes).CopyTo(bytes, 34);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = isBlack(x, y) ? (byte)0 : (byte)255;
                var offset = 54 + (height - 1 - y) * rowBytes + x * 3;
                bytes[offset] = value;
                bytes[offset + 1] = value;
                bytes[offset + 2] = value;
            }
        }

        return bytes;
    }

    private static string AsDataUrl(byte[] bytes) => $"data:image/bmp;base64,{Convert.ToBase64String(bytes)}";
}
