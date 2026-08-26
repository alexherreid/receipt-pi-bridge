using System.Text;

namespace Ncr7198.PiBridge;

public sealed class NcrReceipt
{
    private readonly List<byte> _bytes = [];

    public NcrReceipt Initialize()
    {
        _bytes.Add(0x10);
        return this;
    }

    public NcrReceipt Compressed(bool enabled)
    {
        _bytes.AddRange([0x1B, 0x16, enabled ? (byte)0x01 : (byte)0x00]);
        return this;
    }

    public NcrReceipt Line(string value = "")
    {
        _bytes.AddRange(Encoding.ASCII.GetBytes(value));
        _bytes.AddRange([0x0D, 0x0A]);
        return this;
    }

    public NcrReceipt Feed(int lines)
    {
        if (lines is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(lines), "Use 0-10 lines.");

        for (var index = 0; index < lines; index++) Line();
        return this;
    }

    public NcrReceipt Logo(RenderedLogo logo)
    {
        _bytes.AddRange([0x1B, 0x61, 0x01]);
        _bytes.AddRange([0x1B, 0x33, 0x30]);

        for (var bandTop = 0; bandTop < logo.Height; bandTop += 24)
        {
            _bytes.AddRange([0x1B, 0x2A, 0x21, (byte)(logo.Width & 0xFF), (byte)(logo.Width >> 8)]);
            for (var x = 0; x < logo.Width; x++)
            {
                for (var byteIndex = 0; byteIndex < 3; byteIndex++)
                {
                    byte packed = 0;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        var y = bandTop + byteIndex * 8 + bit;
                        if (y < logo.Height && logo.Pixels[y * logo.Width + x] != 0)
                            packed |= (byte)(0x80 >> bit);
                    }
                    _bytes.Add(packed);
                }
            }
            _bytes.Add(0x0A);
        }

        _bytes.AddRange([0x1B, 0x33, 0x36]);
        _bytes.AddRange([0x1B, 0x61, 0x00]);
        return this;
    }

    public NcrReceipt Cut()
    {
        _bytes.AddRange([0x1D, 0x56, 0x41, 0x00]);
        return this;
    }

    public byte[] Build() => [.. _bytes];
}
