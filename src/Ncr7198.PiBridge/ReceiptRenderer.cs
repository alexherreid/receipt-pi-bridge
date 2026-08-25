using System.Security.Cryptography;

namespace Ncr7198.PiBridge;

public sealed class ReceiptRenderer
{
    public const int StandardWidth = 44;
    public const int CompressedWidth = 56;
    public const int MaxContentCharacters = 16 * 1024;
    public const int MaxRenderedLines = 500;

    public RenderedPrintJob Render(PrintRequest request)
    {
        if (request is null) throw new PrintValidationException("Request body is required.");
        ValidateRanges(request);

        var width = request.Compressed ? CompressedWidth : StandardWidth;
        var renderedLines = request.Lines is not null
            ? RenderLiteralLines(request.Lines, request.Wrap, width)
            : RenderContent(request.Content, request.Wrap, width);

        var effectiveCut = request.Cut || request.Copies > 1;
        var cutForced = !request.Cut && request.Copies > 1;
        var printedLineCount = checked((request.PrePrintLines + renderedLines.Count + request.PostPrintLines) * request.Copies);
        if (printedLineCount > MaxRenderedLines)
            throw new PrintValidationException($"Rendered output is {printedLineCount} lines; the maximum is {MaxRenderedLines} including feeds and copies.");

        var receipt = new NcrReceipt();
        var preview = new List<string>();
        for (var copy = 0; copy < request.Copies; copy++)
        {
            receipt.Initialize().Compressed(request.Compressed).Feed(request.PrePrintLines);
            preview.AddRange(Enumerable.Repeat(string.Empty, request.PrePrintLines));
            foreach (var line in renderedLines)
            {
                receipt.Line(line);
                preview.Add(line);
            }
            receipt.Feed(request.PostPrintLines);
            preview.AddRange(Enumerable.Repeat(string.Empty, request.PostPrintLines));
            if (effectiveCut)
            {
                receipt.Cut();
                preview.Add("[CUT]");
            }
            receipt.Compressed(false);
        }

        var bytes = receipt.Build();
        return new RenderedPrintJob(bytes, [.. preview], Convert.ToHexString(SHA256.HashData(bytes)),
            NormalizePrintId(request.PrintId), request.Copies, request.Cut, effectiveCut, cutForced);
    }

    private static void ValidateRanges(PrintRequest request)
    {
        if (request.PrePrintLines is < 0 or > 10)
            throw new PrintValidationException("prePrintLines must be between 0 and 10.");
        if (request.PostPrintLines is < 0 or > 10)
            throw new PrintValidationException("postPrintLines must be between 0 and 10.");
        if (request.Copies is < 1 or > 3)
            throw new PrintValidationException("copies must be between 1 and 3.");
        if (request.Wrap is not ("none" or "word"))
            throw new PrintValidationException("wrap must be 'none' or 'word'.");
        if (NormalizePrintId(request.PrintId) is { Length: > 128 })
            throw new PrintValidationException("printId cannot exceed 128 characters.");
    }

    private static List<string> RenderLiteralLines(string[] lines, string wrap, int width)
    {
        if (wrap != "none") throw new PrintValidationException("wrap must be 'none' when lines is supplied.");
        if (lines.Length == 0) throw new PrintValidationException("lines cannot be an empty array.");

        var characterCount = lines.Sum(line => (line ?? string.Empty).Length) + Math.Max(0, lines.Length - 1);
        if (characterCount > MaxContentCharacters)
            throw new PrintValidationException($"Receipt content exceeds {MaxContentCharacters} characters.");

        var result = new List<string>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index] ?? throw new PrintValidationException($"lines[{index}] cannot be null.");
            ValidatePrintableAscii(line, $"lines[{index}]", false);
            ValidateWidth(line, width, $"lines[{index}]");
            result.Add(line);
        }
        return result;
    }

    private static List<string> RenderContent(string? content, string wrap, int width)
    {
        if (content is null) throw new PrintValidationException("Supply lines or content; both are null.");
        if (content.Length == 0) throw new PrintValidationException("content cannot be empty.");
        if (content.Length > MaxContentCharacters)
            throw new PrintValidationException($"Receipt content exceeds {MaxContentCharacters} characters.");

        ValidatePrintableAscii(content, "content", true);
        var explicitLines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => line.TrimStart(' ')).ToArray();
        if (wrap == "none")
        {
            for (var index = 0; index < explicitLines.Length; index++)
                ValidateWidth(explicitLines[index], width, $"content line {index + 1}");
            return [.. explicitLines];
        }

        var wrapped = new List<string>();
        for (var index = 0; index < explicitLines.Length; index++)
            WordWrap(explicitLines[index], width, index + 1, wrapped);
        return wrapped;
    }

    private static void WordWrap(string line, int width, int sourceLineNumber, List<string> output)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            output.Add(string.Empty);
            return;
        }

        var current = words[0];
        ValidateWord(current, width, sourceLineNumber);
        foreach (var word in words.Skip(1))
        {
            ValidateWord(word, width, sourceLineNumber);
            if (current.Length + 1 + word.Length <= width) current += " " + word;
            else
            {
                output.Add(current);
                current = word;
            }
        }
        output.Add(current);
    }

    private static void ValidateWord(string word, int width, int sourceLineNumber)
    {
        if (word.Length > width)
            throw new PrintValidationException($"Word '{word}' on content line {sourceLineNumber} is {word.Length} characters; maximum width is {width}.");
    }

    private static void ValidatePrintableAscii(string value, string field, bool allowNewLines)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is >= ' ' and <= '~') continue;
            if (allowNewLines && character is '\r' or '\n') continue;

            var display = char.IsControl(character) ? "control character" : $"'{character}'";
            throw new PrintValidationException($"Unsupported {display} U+{(int)character:X4} in {field} at character {index + 1}. Only printable ASCII U+0020-U+007E is supported; content may also contain CR/LF line breaks.");
        }
    }

    private static void ValidateWidth(string line, int width, string location)
    {
        if (line.Length > width)
            throw new PrintValidationException($"{location} is {line.Length} characters; maximum width is {width}.");
    }

    private static string? NormalizePrintId(string? printId) => string.IsNullOrWhiteSpace(printId) ? null : printId.Trim();
}
