using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MdViewer.Services;

/// <summary>
/// 使用公共翻译通道把英文 Markdown 翻译成简体中文。
/// 适合个人查看器；如果要稳定翻译长规范文档，建议替换为正式的 Azure Translator/DeepL API。
/// </summary>
public class PublicTranslationService : ITranslationService
{
    private static readonly HttpClient Http = CreateClient();
    private const int MaxBatchCharacters = 4_000;
    private const int MaxBatchItems = 40;

    private string? _edgeToken;
    private DateTime _edgeTokenExpiresAt;

    private sealed record MarkdownPart(string Prefix, string Content, bool Translate);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    }

    public async Task<string> TranslateToChineseAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var parts = ParseMarkdown(text);
        var translations = await TranslatePartsAsync(parts, ct);

        var sb = new StringBuilder();
        var translatedIndex = 0;
        foreach (var part in parts)
        {
            if (!part.Translate)
            {
                sb.AppendLine(part.Prefix + part.Content);
                continue;
            }

            sb.AppendLine(part.Prefix + translations[translatedIndex++]);
        }

        return sb.ToString();
    }

    private static List<MarkdownPart> ParseMarkdown(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var parts = new List<MarkdownPart>(lines.Length);
        var inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                inCodeBlock = !inCodeBlock;
                parts.Add(new MarkdownPart(string.Empty, line, false));
                continue;
            }

            if (ShouldKeepOriginal(line, trimmed, inCodeBlock))
            {
                parts.Add(new MarkdownPart(string.Empty, line, false));
                continue;
            }

            var (prefix, content) = SplitMarkdownPrefix(line);
            parts.Add(new MarkdownPart(prefix, content, true));
        }

        return parts;
    }

    private async Task<List<string>> TranslatePartsAsync(List<MarkdownPart> parts, CancellationToken ct)
    {
        var sourceTexts = parts.Where(p => p.Translate).Select(p => p.Content).ToList();
        var result = new List<string>(sourceTexts.Count);

        for (var i = 0; i < sourceTexts.Count;)
        {
            var batch = new List<string>();
            var chars = 0;

            while (i < sourceTexts.Count && batch.Count < MaxBatchItems)
            {
                var next = sourceTexts[i];
                if (batch.Count > 0 && chars + next.Length > MaxBatchCharacters)
                    break;

                batch.Add(next);
                chars += next.Length;
                i++;
            }

            result.AddRange(await TranslateBatchWithFallbackAsync(batch, ct));
        }

        return result;
    }

    private static bool ShouldKeepOriginal(string line, string trimmed, bool inCodeBlock)
    {
        if (inCodeBlock || string.IsNullOrWhiteSpace(line))
            return true;

        if (!HasAsciiLetter(line))
            return true;

        if (trimmed.StartsWith("![") || trimmed.StartsWith("[") || trimmed.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
            return true;

        if (trimmed.Trim('|', ' ', '-', ':').Length == 0)
            return true;

        return false;
    }

    private static (string prefix, string content) SplitMarkdownPrefix(string line)
    {
        var i = 0;
        while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++;

        var markStart = i;
        while (i < line.Length && (line[i] == '#' || line[i] == '>' || line[i] == '-' || line[i] == '*' || line[i] == '+'))
            i++;

        var digitStart = i;
        while (i < line.Length && char.IsDigit(line[i])) i++;
        if (i > digitStart && i < line.Length && (line[i] == '.' || line[i] == ')'))
            i++;

        if (i < line.Length && line[i] == ' ')
            i++;

        if (i == markStart)
            return (line[..markStart], line[markStart..]);

        return (line[..i], line[i..]);
    }

    private async Task<IReadOnlyList<string>> TranslateBatchWithFallbackAsync(IReadOnlyList<string> batch, CancellationToken ct)
    {
        try
        {
            return await CallEdgeBatchAsync(batch, ct);
        }
        catch
        {
            var result = new List<string>(batch.Count);
            foreach (var text in batch)
                result.Add(await TranslateSingleWithFallbackAsync(text, ct));
            return result;
        }
    }

    private async Task<string> TranslateSingleWithFallbackAsync(string text, CancellationToken ct)
    {
        var errors = new List<Exception>();

        foreach (var translate in new Func<string, CancellationToken, Task<string>>[]
                 {
                     CallGoogleAsync,
                     CallMyMemoryAsync,
                 })
        {
            try
            {
                return await translate(text, ct);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        throw new InvalidOperationException("公共翻译接口暂时不可用。", errors.LastOrDefault());
    }

    private async Task<IReadOnlyList<string>> CallEdgeBatchAsync(IReadOnlyList<string> batch, CancellationToken ct)
    {
        var token = await GetEdgeTokenAsync(ct);
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api-edge.cognitive.microsofttranslator.com/translate?api-version=3.0&to=zh-Hans");
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        req.Content = new StringContent(
            JsonSerializer.Serialize(batch.Select(x => new { Text = x })),
            Encoding.UTF8,
            "application/json");

        using var resp = await Http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var result = new List<string>(batch.Count);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            result.Add(item
                .GetProperty("translations")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty);
        }

        return result;
    }

    private async Task<string> GetEdgeTokenAsync(CancellationToken ct)
    {
        if (_edgeToken is not null && DateTime.Now < _edgeTokenExpiresAt)
            return _edgeToken;

        _edgeToken = await Http.GetStringAsync("https://edge.microsoft.com/translate/auth", ct);
        _edgeTokenExpiresAt = DateTime.Now.AddMinutes(8);
        return _edgeToken;
    }

    private static async Task<string> CallGoogleAsync(string text, CancellationToken ct)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx"
                + "&sl=auto&tl=zh-CN&dt=t&q=" + Uri.EscapeDataString(text);

        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var segments = doc.RootElement[0];
        var sb = new StringBuilder();
        foreach (var seg in segments.EnumerateArray())
        {
            var piece = seg[0].GetString();
            if (piece is not null)
                sb.Append(piece);
        }

        return sb.ToString();
    }

    private static async Task<string> CallMyMemoryAsync(string text, CancellationToken ct)
    {
        var url = "https://api.mymemory.translated.net/get?langpair=en|zh-CN&q="
                + Uri.EscapeDataString(text);

        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("responseData")
            .GetProperty("translatedText")
            .GetString() ?? text;
    }

    private static bool HasAsciiLetter(string s)
    {
        foreach (var c in s)
        {
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                return true;
        }

        return false;
    }
}
