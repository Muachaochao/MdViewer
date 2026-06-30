# Markdown 查看器

一个基于 WPF、Prism、WebView2 和 Markdig 开发的桌面 Markdown 查看器。项目目标是把常见 Markdown 文档、说明书、协议文档和 GitHub 项目文档，以更接近阅读器的方式打开、管理、翻译和导出。

## 功能特性

- 打开单个 `.md` / `.markdown` 文件
- 打开文件夹并以树形目录浏览 Markdown 文档
- 收藏、最近打开、失效记录清理
- Markdown 渲染、目录导航、代码高亮
- 支持本地相对图片显示，适合阅读 GitHub 仓库文档
- 当前文档搜索，支持命中高亮
- 图片点击放大预览
- 字号、行距、布局宽度调整
- 浅色、深色、跟随系统三档主题
- 中英文翻译、双语对照、翻译缓存
- 打印预览
- 导出 PDF，支持打印专用目录页，并优先生成 PDF 目录书签

## 技术栈

- `.NET 9`
- `WPF`
- `Prism.DryIoc`
- `WebView2`
- `Markdig`
- `SqlSugarCore`
- `SQLite`
- `highlight.js`

## 项目结构

```text
MdViewer/
  Assets/                 Markdown 样式、highlight.js、示例文件
  Events/                 Prism 事件
  Models/                 最近文件、收藏、翻译缓存等数据模型
  Services/               文件、渲染、数据库、翻译、缓存、监听服务
  ViewModels/             主窗口、侧边栏、Markdown 阅读区 ViewModel
  Views/                  WPF 视图
  App.xaml                全局主题资源和控件样式
  MdViewer.csproj         项目文件
```

## 快速运行

环境要求：

- Windows 10 / Windows 11
- .NET 9 SDK
- Microsoft Edge WebView2 Runtime

命令行运行：

```powershell
dotnet build .\MdViewer\MdViewer.csproj
dotnet run --project .\MdViewer\MdViewer.csproj
```

打开测试文档：

```powershell
dotnet run --project .\MdViewer\MdViewer.csproj -- .\测试文档.md
```

## 使用说明

顶部工具栏分为几个区域：

- `打开`：选择单个 Markdown 文件
- `收藏`：收藏或取消收藏当前文件
- `主题`：切换浅色、深色、跟随系统
- `阅读`：调整字号、行距、重置布局
- `打印`：打开 WebView2 打印预览
- `导出 PDF`：导出当前文档为 PDF
- `更多`：翻译中文、双语对照

左侧栏用于管理文档：

- 打开或关闭工作区文件夹
- 查看文件夹内 Markdown 树
- 查看收藏和最近打开
- 按分组清理失效记录

## Markdown 图片路径

项目通过 WebView2 虚拟主机映射当前 Markdown 文件所在目录，让文档中的相对图片可以正常显示。例如，Markdown 中可以这样写：

```markdown
![设备图片](images/device.png)
```

上面是写法示例。实际显示图片时，图片文件必须真实存在，并且路径要相对当前 Markdown 文件正确。

仓库中的测试图片示例：

![Markdown 相对图片示例](imgtest/images/demo.png)

这张图片来自：

```text
imgtest/images/demo.png
```

## 翻译功能

应用支持：

- 原文 / 中文译文切换
- 双语对照模式
- 翻译缓存

缓存会记录文件路径、修改时间和译文内容，避免同一文档重复请求翻译。

## PDF 导出

导出 PDF 时会使用打印专用样式：

- 隐藏悬浮目录、搜索框、图片预览等界面控件
- 添加 PDF 专用目录页
- 优先通过 Chromium DevTools 生成 PDF 目录书签
- 关闭浏览器默认页眉页脚，避免出现 `about:blank`、时间、页码等杂项

## 学习重点

这个项目适合学习：

- WPF MVVM 项目组织
- Prism 事件聚合器解耦模块
- WebView2 嵌入 HTML 渲染
- Markdig Markdown 转 HTML
- 本地资源映射和相对图片处理
- SQLite + SqlSugar 做轻量数据持久化
- 桌面应用主题切换和控件样式管理

## 后续可扩展方向

- 导出 HTML
- 更完整的 PDF 书签和页码目录
- 翻译服务设置页，支持自定义 API Key
- 更精致的文件夹树图标和选中态
- 多标签页阅读
- 文档历史版本比较
