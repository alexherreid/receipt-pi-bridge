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

    public NcrReceipt Cut()
    {
        _bytes.AddRange([0x1D, 0x56, 0x41, 0x00]);
        return this;
    }

    public byte[] Build() => [.. _bytes];
}
