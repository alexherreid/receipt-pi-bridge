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

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void FeedBounds_AreEnforced(int lines)
    {
        Assert.Throws<PrintValidationException>(() =>
            _renderer.Render(new PrintRequest { Content = "Test", PrePrintLines = lines }));
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
        public string Description => "test";
        public int WriteCount { get; private set; }
        public bool IsAvailable() => true;
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
