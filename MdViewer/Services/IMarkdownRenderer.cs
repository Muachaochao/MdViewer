namespace MdViewer.Services;

/// <summary>把 Markdown 文本渲染成可在 WebView2 中显示的完整 HTML。</summary>
public interface IMarkdownRenderer
{
    /// <summary>将 Markdown 源文本转换为带样式、目录和代码高亮的完整 HTML。</summary>
    /// <param name="markdown">Markdown 源文本。</param>
    /// <param name="darkTheme">是否使用暗色主题。</param>
    /// <param name="fontScale">正文基础字号倍率。</param>
    /// <param name="lineHeight">正文行距。</param>
    /// <param name="resetLayout">是否在页面加载时清除正文宽度和目录宽度记忆。</param>
    string RenderToHtml(
        string markdown,
        bool darkTheme = false,
        double fontScale = 1.0,
        double lineHeight = 1.6,
        bool resetLayout = false);
}
