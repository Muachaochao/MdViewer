using Prism.Events;

namespace MdViewer.Events;

/// <summary>请求打开并渲染某个 Markdown 文件，负载为文件完整路径。</summary>
public class OpenMarkdownEvent : PubSubEvent<string>
{
}

/// <summary>最近列表或收藏列表发生变化，通知侧边栏刷新。</summary>
public class FileListChangedEvent : PubSubEvent
{
}

/// <summary>当前查看的文件被外部修改，负载为文件路径。</summary>
public class FileChangedEvent : PubSubEvent<string>
{
}

/// <summary>主题切换，负载 true=暗色，false=亮色。</summary>
public class ThemeChangedEvent : PubSubEvent<bool>
{
}

/// <summary>请求翻译当前文档，作为原文/译文切换触发。</summary>
public class TranslateRequestEvent : PubSubEvent
{
}

/// <summary>请求双语对照显示当前文档。</summary>
public class BilingualRequestEvent : PubSubEvent
{
}

/// <summary>当前文档翻译显示状态，供工具栏按钮更新文案。</summary>
public class TranslationStateChangedEvent : PubSubEvent<TranslationState>
{
}

public record TranslationState(bool IsTranslated, bool IsTranslating, bool IsBilingual = false);

/// <summary>阅读排版设置变更，负载为字号倍率和行距。</summary>
public class ReadingSettingsChangedEvent : PubSubEvent<ReadingSettings>
{
}

public record ReadingSettings(double FontScale, double LineHeight);

/// <summary>请求重置阅读布局，包括主题、字号、行距、正文宽度和目录宽度。</summary>
public class ResetLayoutEvent : PubSubEvent
{
}

/// <summary>请求打印当前渲染文档。</summary>
public class PrintDocumentEvent : PubSubEvent
{
}

/// <summary>请求把当前渲染文档导出为 PDF。</summary>
public class ExportPdfEvent : PubSubEvent
{
}
