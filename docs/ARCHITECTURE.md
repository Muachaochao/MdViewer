# 项目架构说明

本文档用于帮助学习者理解 Markdown 查看器的主要设计和代码组织。

## 总体架构

应用采用 WPF + Prism 的 MVVM 结构：

```text
View       负责界面展示和少量 UI 行为
ViewModel 负责状态、命令和事件协调
Service   负责文件、数据库、渲染、翻译等业务能力
Model     负责数据库实体和数据结构
Event     负责跨模块通信
```

核心思想是：主窗口、侧边栏、Markdown 阅读区互相不直接调用，而是通过 Prism `IEventAggregator` 通信。

## 主要模块

### MainWindow

位置：

```text
MdViewer/Views/MainWindow.xaml
MdViewer/ViewModels/MainWindowViewModel.cs
```

职责：

- 顶部工具栏
- 当前文件名显示
- 主题切换
- 阅读设置菜单
- 打印、导出 PDF、翻译入口
- 注册 Prism 区域

### SidebarView

位置：

```text
MdViewer/Views/SidebarView.xaml
MdViewer/ViewModels/SidebarViewModel.cs
```

职责：

- 文件夹工作区树
- 收藏列表
- 最近打开列表
- 清理失效记录
- 文件右键菜单

### MarkdownView

位置：

```text
MdViewer/Views/MarkdownView.xaml
MdViewer/Views/MarkdownView.xaml.cs
MdViewer/ViewModels/MarkdownViewModel.cs
```

职责：

- 初始化 WebView2
- 显示 Markdown 渲染后的 HTML
- 映射本地图片目录
- 打印和导出 PDF
- 响应主题、字号、行距、翻译状态变化

## Markdown 渲染流程

1. 用户打开 Markdown 文件
2. `OpenMarkdownEvent` 发布文件路径
3. `MarkdownViewModel.Load` 读取文件
4. `MarkdownRenderer.RenderToHtml` 使用 Markdig 转 HTML
5. 注入 CSS、highlight.js、目录、搜索、图片预览脚本
6. `MarkdownView` 把 HTML 加载到 WebView2

相关文件：

```text
MdViewer/Services/MarkdownRenderer.cs
MdViewer/Assets/markdown.css
```

## 本地图片显示

Markdown 中常见相对图片：

```markdown
![image](./assets/demo.png)
```

WebView2 默认不能直接按 Markdown 文件目录解析这些路径。项目使用：

```csharp
SetVirtualHostNameToFolderMapping("mdviewer.assets", currentMarkdownDirectory, Allow)
```

然后在 HTML 中把相对图片地址转换到：

```text
https://mdviewer.assets/...
```

这样就能读取当前文档目录下的图片资源。

## 数据持久化

项目使用 SqlSugar + SQLite 保存轻量数据：

- 最近打开
- 收藏
- 翻译缓存

相关文件：

```text
MdViewer/Models/
MdViewer/Services/RecentFileService.cs
MdViewer/Services/TranslationCacheService.cs
```

## 主题系统

主题入口在 `MainWindowViewModel`：

- 浅色
- 深色
- 跟随系统

WPF 外壳通过动态资源切换：

```xaml
Background="{DynamicResource AppBackgroundBrush}"
```

Markdown 正文通过 `ThemeChangedEvent` 通知 `MarkdownViewModel` 重新渲染 HTML，并选择对应的代码高亮主题。

## 阅读设置

阅读设置包括：

- 字号
- 行距
- 布局重置

设置通过 `ReadingSettingsChangedEvent` 传递给 Markdown 阅读区。

```csharp
public record ReadingSettings(double FontScale, double LineHeight);
```

## 翻译和双语

翻译显示模式包括：

- 原文
- 译文
- 双语对照

`MarkdownViewModel` 负责协调翻译、缓存和渲染模式。

## PDF 导出

导出优先使用 WebView2 DevTools：

```text
Page.printToPDF
```

这样可以控制：

- 关闭页眉页脚
- 打印背景
- 使用 CSS 页面尺寸
- 尝试生成 PDF 目录书签

如果 DevTools 导出失败，会回退到 WebView2 `PrintToPdfAsync`。

## 适合继续练习的任务

- 给文件夹树添加更精细的图标
- 增加导出 HTML
- 增加设置页
- 支持多标签页
- 支持更多翻译服务
- 给服务层补单元测试
