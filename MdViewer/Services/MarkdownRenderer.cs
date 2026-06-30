using System.Globalization;
using System.IO;
using Markdig;

namespace MdViewer.Services;

/// <summary>用 Markdig 把 Markdown 转 HTML，并内联样式、TOC、搜索、图片预览和 highlight.js。</summary>
public class MarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;
    private readonly string _css;
    private readonly string _hljsJs;
    private readonly string _hljsLight;
    private readonly string _hljsDark;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers()
            .Build();

        var baseDir = AppContext.BaseDirectory;
        _css = ReadAsset(Path.Combine(baseDir, "Assets", "markdown.css"));
        _hljsJs = ReadAsset(Path.Combine(baseDir, "Assets", "lib", "highlight.min.js"));
        _hljsLight = ReadAsset(Path.Combine(baseDir, "Assets", "lib", "github.min.css"));
        _hljsDark = ReadAsset(Path.Combine(baseDir, "Assets", "lib", "github-dark.min.css"));
    }

    private static string ReadAsset(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    public string RenderToHtml(
        string markdown,
        bool darkTheme = false,
        double fontScale = 1.0,
        double lineHeight = 1.6,
        bool resetLayout = false)
    {
        var body = Markdown.ToHtml(markdown ?? string.Empty, _pipeline);
        var bodyClass = darkTheme ? "markdown-body dark" : "markdown-body";
        var hljsTheme = darkTheme ? _hljsDark : _hljsLight;
        var pageClass = darkTheme ? "dark" : string.Empty;
        var fontSize = (16 * fontScale).ToString("0.##", CultureInfo.InvariantCulture);
        var lineHeightValue = lineHeight.ToString("0.##", CultureInfo.InvariantCulture);
        var resetLayoutScript = resetLayout ? "true" : "false";

        return $$"""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <style>
                body {
                  --reader-font-size: {{fontSize}}px;
                  --reader-line-height: {{lineHeightValue}};
                }
              </style>
              <style>{{hljsTheme}}</style>
              <style>{{_css}}</style>
            </head>
            <body class="{{pageClass}}">
              <nav id="toc"></nav>
              <section id="printToc" class="print-toc"></section>
              <div id="docSearch" class="doc-search" hidden>
                <input id="docSearchInput" type="text" placeholder="搜索当前文档" autocomplete="off">
                <span id="docSearchCount">0/0</span>
                <button id="docSearchPrev" type="button" title="上一个">↑</button>
                <button id="docSearchNext" type="button" title="下一个">↓</button>
                <button id="docSearchClose" type="button" title="关闭">×</button>
              </div>
              <div id="imagePreview" class="image-preview" hidden>
                <div class="image-preview-toolbar">
                  <button id="imageZoomOut" type="button">-</button>
                  <button id="imageZoomReset" type="button">100%</button>
                  <button id="imageZoomIn" type="button">+</button>
                  <button id="imagePreviewClose" type="button">关闭</button>
                </div>
                <img id="imagePreviewImg" alt="图片预览">
              </div>
              <article class="{{bodyClass}}">
            {{body}}
              </article>
              <div id="contentResizeHandle" class="content-resize-handle" title="拖动调整正文宽度"></div>
              <script>{{_hljsJs}}</script>
              <script>
                function clamp(value, min, max) {
                  return Math.max(min, Math.min(max, value));
                }

                function getStoredNumber(key, fallback) {
                  try {
                    var value = Number(localStorage.getItem(key));
                    return Number.isFinite(value) && value > 0 ? value : fallback;
                  } catch (e) {
                    return fallback;
                  }
                }

                function setStoredNumber(key, value) {
                  try { localStorage.setItem(key, String(Math.round(value))); } catch (e) {}
                }

                if ({{resetLayoutScript}}) {
                  try {
                    localStorage.removeItem('mdviewer.contentWidth');
                    localStorage.removeItem('mdviewer.tocWidth');
                  } catch (e) {}
                }

                var layout = {
                  contentWidth: getStoredNumber('mdviewer.contentWidth', 980),
                  tocWidth: getStoredNumber('mdviewer.tocWidth', 240)
                };

                function getMaxContentWidth() {
                  return Math.max(560, window.innerWidth - 32);
                }

                function getMaxTocWidth() {
                  return Math.min(560, Math.max(180, window.innerWidth - 80));
                }

                function applyLayout() {
                  document.body.style.setProperty('--content-width',
                    clamp(layout.contentWidth, 560, getMaxContentWidth()) + 'px');
                  document.body.style.setProperty('--toc-width',
                    clamp(layout.tocWidth, 180, getMaxTocWidth()) + 'px');
                  updateContentHandle();
                }

                function updateContentHandle() {
                  var article = document.querySelector('.markdown-body');
                  var handle = document.getElementById('contentResizeHandle');
                  if (!article || !handle) return;
                  var rect = article.getBoundingClientRect();
                  var left = clamp(rect.right + 4, 0, Math.max(0, window.innerWidth - 12));
                  handle.style.left = left + 'px';
                }

                function initContentResize() {
                  var article = document.querySelector('.markdown-body');
                  var handle = document.getElementById('contentResizeHandle');
                  if (!article || !handle) return;

                  var startX = 0;
                  var startWidth = 0;

                  handle.addEventListener('mousedown', function (event) {
                    event.preventDefault();
                    startX = event.clientX;
                    startWidth = article.getBoundingClientRect().width;
                    document.body.classList.add('resizing-content');
                  });

                  document.addEventListener('mousemove', function (event) {
                    if (!document.body.classList.contains('resizing-content')) return;
                    layout.contentWidth = clamp(startWidth + (event.clientX - startX) * 2, 560, getMaxContentWidth());
                    document.body.style.setProperty('--content-width', layout.contentWidth + 'px');
                    setStoredNumber('mdviewer.contentWidth', layout.contentWidth);
                    updateContentHandle();
                  });

                  document.addEventListener('mouseup', function () {
                    document.body.classList.remove('resizing-content');
                  });

                  window.addEventListener('resize', applyLayout);
                  window.addEventListener('scroll', updateContentHandle, { passive: true });
                  updateContentHandle();
                }

                function setupTocShell(toc) {
                  toc.innerHTML = '';

                  var handle = document.createElement('div');
                  handle.className = 'toc-resize-handle';
                  handle.title = '拖动调整目录宽度';
                  toc.appendChild(handle);

                  var content = document.createElement('div');
                  content.className = 'toc-content';
                  toc.appendChild(content);

                  return content;
                }

                function initTocResize() {
                  var toc = document.getElementById('toc');
                  var handle = toc && toc.querySelector('.toc-resize-handle');
                  if (!toc || !handle) return;

                  var startX = 0;
                  var startWidth = 0;
                  handle.addEventListener('mousedown', function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    startX = event.clientX;
                    startWidth = toc.getBoundingClientRect().width;
                    document.body.classList.add('resizing-toc');
                  });

                  document.addEventListener('mousemove', function (event) {
                    if (!document.body.classList.contains('resizing-toc')) return;
                    layout.tocWidth = clamp(startWidth + (startX - event.clientX), 180, getMaxTocWidth());
                    document.body.style.setProperty('--toc-width', layout.tocWidth + 'px');
                    setStoredNumber('mdviewer.tocWidth', layout.tocWidth);
                  });

                  document.addEventListener('mouseup', function () {
                    document.body.classList.remove('resizing-toc');
                  });
                }

                function initCodeBlocks() {
                  document.querySelectorAll('pre code').forEach(function (el) {
                    try { hljs.highlightElement(el); } catch (e) {}
                  });

                  document.querySelectorAll('pre').forEach(function (pre) {
                    var btn = document.createElement('button');
                    btn.className = 'copy-btn';
                    btn.textContent = '复制';
                    btn.onclick = function () {
                      var code = pre.querySelector('code');
                      navigator.clipboard.writeText(code ? code.innerText : pre.innerText);
                      btn.textContent = '已复制';
                      setTimeout(function () { btn.textContent = '复制'; }, 1200);
                    };
                    pre.appendChild(btn);
                  });
                }

                function scrollToHeading(id) {
                  var target = document.getElementById(id);
                  if (!target) return;
                  target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }

                function initAnchors() {
                  document.querySelectorAll('.markdown-body a[href^="#"]').forEach(function (link) {
                    link.addEventListener('click', function (event) {
                      event.preventDefault();
                      scrollToHeading(link.getAttribute('href').substring(1));
                    });
                  });
                }

                function isVirtualHostUrl(value) {
                  return /^https:\/\/mdviewer\.assets\//i.test(value || '');
                }

                function isAbsoluteAssetUrl(value) {
                  return /^(https?:|file:|data:|blob:|mailto:|#)/i.test(value || '');
                }

                function toVirtualAssetUrl(value) {
                  return 'https://mdviewer.assets/' + String(value || '')
                    .replace(/\\/g, '/')
                    .replace(/^[./\\]+/, '');
                }

                function initRelativeImages() {
                  document.querySelectorAll('.markdown-body img').forEach(function (img) {
                    var raw = img.getAttribute('src');
                    if (!raw || isAbsoluteAssetUrl(raw) || isVirtualHostUrl(raw)) return;
                    img.src = toVirtualAssetUrl(raw);
                  });
                }

                function initPrintToc(headings) {
                  var printToc = document.getElementById('printToc');
                  if (!printToc) return;
                  printToc.innerHTML = '';
                  if (!headings || headings.length <= 1) {
                    printToc.classList.add('is-empty');
                    printToc.style.display = 'none';
                    return;
                  }
                  printToc.classList.remove('is-empty');
                  printToc.style.display = '';

                  var title = document.createElement('h1');
                  title.textContent = '目录';
                  printToc.appendChild(title);

                  headings.forEach(function (h) {
                    var link = document.createElement('a');
                    link.className = 'print-toc-' + h.tagName.toLowerCase();
                    link.href = '#' + h.id;
                    link.textContent = h.textContent;
                    printToc.appendChild(link);
                  });
                }

                function initToc() {
                  var headings = document.querySelectorAll('.markdown-body h1, .markdown-body h2, .markdown-body h3');
                  var toc = document.getElementById('toc');
                  if (headings.length <= 1) {
                    toc.style.display = 'none';
                    initPrintToc(headings);
                    return;
                  }

                  var tocContent = setupTocShell(toc);
                  var title = document.createElement('div');
                  title.className = 'toc-title';
                  title.textContent = '目录';
                  tocContent.appendChild(title);

                  headings.forEach(function (h) {
                    if (!h.id) h.id = 'h_' + Math.random().toString(36).slice(2);
                    var link = document.createElement('a');
                    link.className = 'toc-' + h.tagName.toLowerCase();
                    link.href = '#';
                    link.textContent = h.textContent;
                    link.addEventListener('click', function (event) {
                      event.preventDefault();
                      scrollToHeading(h.id);
                    });
                    tocContent.appendChild(link);
                  });
                  initTocResize();
                  initPrintToc(headings);
                }

                var searchState = { hits: [], index: -1 };

                function clearSearchMarks() {
                  document.querySelectorAll('mark.search-hit').forEach(function (mark) {
                    var text = document.createTextNode(mark.textContent || '');
                    mark.parentNode.replaceChild(text, mark);
                    text.parentNode.normalize();
                  });
                  searchState.hits = [];
                  searchState.index = -1;
                  updateSearchCount();
                }

                function updateSearchCount() {
                  var count = document.getElementById('docSearchCount');
                  if (!count) return;
                  count.textContent = searchState.hits.length
                    ? (searchState.index + 1) + '/' + searchState.hits.length
                    : '0/0';
                }

                function collectTextNodes(root) {
                  var nodes = [];
                  var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, {
                    acceptNode: function (node) {
                      if (!node.nodeValue || !node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
                      var parent = node.parentElement;
                      if (!parent) return NodeFilter.FILTER_REJECT;
                      if (parent.closest('script, style, pre, code, mark, #toc, #docSearch, #imagePreview')) {
                        return NodeFilter.FILTER_REJECT;
                      }
                      return NodeFilter.FILTER_ACCEPT;
                    }
                  });
                  while (walker.nextNode()) nodes.push(walker.currentNode);
                  return nodes;
                }

                function runSearch(term) {
                  clearSearchMarks();
                  if (!term) return;
                  var article = document.querySelector('.markdown-body');
                  var lower = term.toLowerCase();
                  collectTextNodes(article).forEach(function (node) {
                    var text = node.nodeValue;
                    var index = text.toLowerCase().indexOf(lower);
                    if (index < 0) return;

                    var fragment = document.createDocumentFragment();
                    var pos = 0;
                    while (index >= 0) {
                      if (index > pos) fragment.appendChild(document.createTextNode(text.slice(pos, index)));
                      var mark = document.createElement('mark');
                      mark.className = 'search-hit';
                      mark.textContent = text.slice(index, index + term.length);
                      fragment.appendChild(mark);
                      searchState.hits.push(mark);
                      pos = index + term.length;
                      index = text.toLowerCase().indexOf(lower, pos);
                    }
                    if (pos < text.length) fragment.appendChild(document.createTextNode(text.slice(pos)));
                    node.parentNode.replaceChild(fragment, node);
                  });
                  searchState.index = searchState.hits.length ? 0 : -1;
                  focusSearchHit(0);
                }

                function focusSearchHit(delta) {
                  if (!searchState.hits.length) {
                    updateSearchCount();
                    return;
                  }
                  searchState.hits.forEach(function (hit) { hit.classList.remove('current'); });
                  searchState.index = (searchState.index + delta + searchState.hits.length) % searchState.hits.length;
                  var current = searchState.hits[searchState.index];
                  current.classList.add('current');
                  current.scrollIntoView({ behavior: 'smooth', block: 'center' });
                  updateSearchCount();
                }

                function showSearch() {
                  var box = document.getElementById('docSearch');
                  var input = document.getElementById('docSearchInput');
                  box.hidden = false;
                  input.focus();
                  input.select();
                }

                function hideSearch() {
                  document.getElementById('docSearch').hidden = true;
                  clearSearchMarks();
                }

                function initSearch() {
                  var input = document.getElementById('docSearchInput');
                  document.addEventListener('keydown', function (event) {
                    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'f') {
                      event.preventDefault();
                      showSearch();
                    } else if (event.key === 'Escape' && !document.getElementById('docSearch').hidden) {
                      event.preventDefault();
                      hideSearch();
                    }
                  });
                  input.addEventListener('input', function () { runSearch(input.value.trim()); });
                  input.addEventListener('keydown', function (event) {
                    if (event.key === 'Enter') {
                      event.preventDefault();
                      focusSearchHit(event.shiftKey ? -1 : 1);
                    }
                  });
                  document.getElementById('docSearchPrev').onclick = function () { focusSearchHit(-1); };
                  document.getElementById('docSearchNext').onclick = function () { focusSearchHit(1); };
                  document.getElementById('docSearchClose').onclick = hideSearch;
                }

                function initImagePreview() {
                  var overlay = document.getElementById('imagePreview');
                  var preview = document.getElementById('imagePreviewImg');
                  var scale = 1;
                  function applyScale() { preview.style.transform = 'scale(' + scale + ')'; }
                  function close() { overlay.hidden = true; preview.removeAttribute('src'); scale = 1; applyScale(); }
                  function open(src, alt) {
                    preview.src = src;
                    preview.alt = alt || '图片预览';
                    scale = 1;
                    applyScale();
                    overlay.hidden = false;
                  }
                  document.querySelectorAll('.markdown-body img').forEach(function (img) {
                    img.addEventListener('click', function () { open(img.currentSrc || img.src, img.alt); });
                  });
                  overlay.addEventListener('click', function (event) {
                    if (event.target === overlay) close();
                  });
                  overlay.addEventListener('wheel', function (event) {
                    event.preventDefault();
                    scale = clamp(scale + (event.deltaY < 0 ? 0.1 : -0.1), 0.25, 4);
                    applyScale();
                  }, { passive: false });
                  document.getElementById('imagePreviewClose').onclick = close;
                  document.getElementById('imageZoomOut').onclick = function () { scale = clamp(scale - 0.2, 0.25, 4); applyScale(); };
                  document.getElementById('imageZoomIn').onclick = function () { scale = clamp(scale + 0.2, 0.25, 4); applyScale(); };
                  document.getElementById('imageZoomReset').onclick = function () { scale = 1; applyScale(); };
                }

                applyLayout();
                initCodeBlocks();
                initAnchors();
                initRelativeImages();
                initToc();
                initContentResize();
                initSearch();
                initImagePreview();
              </script>
            </body>
            </html>
            """;
    }
}
