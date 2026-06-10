# 🎬 影音智增强管理系统

> 一站式本地影音资产管理 + AI 实时视觉增强 Windows 桌面应用

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-blue)](https://github.com/dotnet/wpf)
[![MVVM](https://img.shields.io/badge/Pattern-MVVM-green)](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
[![SQLite](https://img.shields.io/badge/DB-SQLite-orange)](https://www.sqlite.org/)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)

---

## 📖 项目简介

**影音智增强管理系统** 是一款集 **本地影音资产管理** 与 **AI 实时视觉增强** 于一体的 Windows 桌面应用。它旨在解决用户在观看或整理低光照、有雾、有雨等低质量影像时"看不清、理还乱"的痛点。

系统采用 **插件式增强架构**，初期以高效的 **线性拉伸** 作为基础增强方法，后续可平滑扩展为深度学习模型。结合大语言模型实现内容的智能理解、自动摘要与规范管理，让每一段影像都清晰可见、有序可查。

---

## ✨ 核心功能

### 📁 影音管理
- **文件扫描与导入** — 支持文件夹递归扫描、多文件批量导入，自动识别图片/视频/音频格式
- **元数据提取** — 自动获取文件类型、时长、分辨率、文件大小等信息
- **多维浏览与筛选** — 按类型、关键词、收藏状态等过滤和排序
- **收藏与标签** — 一键收藏/取消收藏，支持批量操作
- **播放记录** — 自动记录播放时间与进度，提供"最近播放"快速入口
- **播放统计** — 统计各媒体文件的播放次数、总时长

### 🎨 图像增强（可扩展架构）
- **插件式增强框架** — 统一 `IRealTimeEnhancer` 接口，支持运行时切换或加载新方法
- **线性拉伸（已实现）** — 像素值线性映射到全动态范围，有效改善低光、低对比度
  - 可调参数：对比度强度 (0.5–2.0)、亮度偏移 (-50–+50)
  - 纯 C# 实现，< 0.1ms/帧，适合实时场景
- **未来可扩展** — 直方图均衡化、CLAHE、MAXIM 深度学习模型等

### 🖥️ 实时屏幕增强
- **透明覆盖窗口** — 以无边框透明窗口悬浮于屏幕最顶层，显示增强后的画面
- **点击穿透** — 增强窗口不阻挡鼠标操作，用户可继续正常使用下层应用
- **全局热键** — F11 快速退出增强模式
- **DXGI + GDI 双模式** — 优先使用 DXGI Desktop Duplication（GPU 零拷贝），自动回退 GDI

### 🔧 离线文件增强
- **右键增强** — 在影音库内右键任意文件选择"AI 增强"
- **预览对比** — 支持加载图片 → 调整参数 → 实时预览增强效果
- **视频逐帧增强** — 通过 FFmpeg 逐帧处理视频，可取消操作
- **批量增强** — 支持多选图片后批量处理
- **自动入库** — 增强后的新文件自动添加到影音库，关联源文件

### 🤖 AI 智能模块
- **AI 对话** — 支持 OpenAI 兼容 API（通义千问 / DeepSeek / Ollama 等），可选中文件进行多模态分析
  - 快捷提示：生成简介、数据摘要、增强建议、美化方案
  - 离线降级：未配置 API 时自动使用模板化本地分析
- **AI 图像生成/编辑** — 支持通义万相 / OpenAI 兼容图像生成 API
- **语音转文字** — 提取音频转写为文字
- **智能推荐** — 基于文件元数据生成画像分析和内容描述

### 🎥 屏幕录制
- **全屏录制** — 支持增强录制（录制时同步叠加增强效果）
- **硬件编码** — NVENC / Intel QSV / AMD AMF / libx264 多编码器自动回退
- **自动入库** — 录制文件自动添加到影音库
- **帧序列兜底** — FFmpeg 编码失败时保留帧序列，用户可手动合成

### 📊 数据统计
- 仪表盘展示：文件总数、图片/视频数量、增强次数、录制次数、播放次数、收藏数量
- 录制历史记录
- 依赖项检查（自动下载 FFmpeg）

---

## 🏗️ 技术架构

```
┌─────────────────────────────────────────────────┐
│                   表现层 (WPF)                    │
│  MainWindow (.xaml)  │  FullscreenEnhanceWindow  │
│  MediaPlayerWindow   │  ImageViewerWindow        │
├─────────────────────────────────────────────────┤
│                ViewModel 层 (MVVM)                │
│  MainViewModel  │  CommunityToolkit.Mvvm         │
├─────────────────────────────────────────────────┤
│               业务逻辑层 (Services)               │
│  DataService  │  FileScanService  │  AiService   │
│  PlaybackService │ ThumbnailService │ Recorder   │
├─────────────────────────────────────────────────┤
│               增强方法层 (Core)                   │
│  IRealTimeEnhancer ← LinearStretchMethod         │
│  EnhancementRegistry (插件注册中心)               │
├─────────────────────────────────────────────────┤
│             数据访问层 (Data + EF Core)           │
│  AppDbContext  │  SQLite  │  6 张数据表           │
├─────────────────────────────────────────────────┤
│              外部依赖                             │
│  FFmpeg  │  NAudio  │  TagLibSharp  │  AI APIs   │
└─────────────────────────────────────────────────┘
```

### 技术栈

| 分类 | 技术 |
|------|------|
| 桌面框架 | .NET 10 WPF |
| 架构模式 | MVVM (CommunityToolkit.Mvvm 8.4) |
| 数据库 | SQLite + Entity Framework Core 10 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 屏幕捕获 | DXGI Desktop Duplication / GDI |
| 视频处理 | FFmpeg (Xabe.FFmpeg) |
| 元数据 | TagLibSharp |
| AI 集成 | OpenAI 兼容 API（通义千问/DeepSeek/Ollama） |
| 图像生成 | 通义万相 / SiliconFlow / OpenAI 兼容 |

---

## 📁 项目结构

```
MediaEnhancer/
├── App.xaml / App.xaml.cs          # 应用入口、DI 容器配置
├── MainWindow.xaml / .cs           # 主窗口（6 页面：统计/管理/增强/录制/AI对话/设置）
├── AssemblyInfo.cs                 # 程序集主题信息
├── MediaEnhancer.csproj            # 项目文件
├── MediaEnhancer.slnx              # 解决方案文件
│
├── Models/                         # 数据实体
│   ├── MediaFile.cs                # 媒体文件
│   ├── PlayHistory.cs              # 播放记录
│   ├── EnhancementLog.cs           # 增强日志
│   ├── Recording.cs                # 录屏记录
│   ├── Favorite.cs                 # 收藏记录
│   ├── Thumbnail.cs                # 缩略图
│   └── ChatMessage.cs              # AI 对话消息
│
├── Core/                           # 核心组件
│   ├── IEnhancementMethod.cs       # 增强方法根接口
│   ├── IRealTimeEnhancer.cs        # 实时增强接口
│   ├── INativeEnhancement.cs       # 离线增强接口
│   ├── IOnnxEnhancement.cs         # ONNX 推理接口（预留）
│   ├── LinearStretchMethod.cs      # 线性拉伸实现
│   ├── EnhancementRegistry.cs      # 增强方法注册中心
│   ├── EnhancementParameter.cs     # 参数定义
│   ├── ParameterMeta.cs            # 参数元数据
│   ├── MediaFileUtils.cs           # 媒体文件工具类
│   └── *Converter.cs               # WPF 值转换器
│
├── Services/                       # 业务服务
│   ├── DataService.cs / IDataService.cs
│   ├── FileScanService.cs / IFileScanService.cs
│   ├── PlaybackService.cs / IPlaybackService.cs
│   ├── ThumbnailService.cs / IThumbnailService.cs
│   ├── AiService.cs               # AI 对话/图像生成服务
│   ├── ScreenRecorder.cs          # 屏幕录制器
│   ├── VideoEnhancer.cs           # 视频增强器
│   └── AppConfig.cs               # 应用配置持久化
│
├── ViewModels/
│   └── MainViewModel.cs            # 主视图模型（约 2100 行，完整业务逻辑）
│
├── Views/                          # 子窗口与控件
│   ├── FullscreenEnhanceWindow     # 全屏增强覆盖窗口
│   ├── MediaPlayerWindow           # 媒体播放器
│   ├── ImageViewerWindow           # 图片查看器
│   ├── FileDetailWindow            # 文件详情
│   ├── DxgiScreenCapture.cs        # DXGI 屏幕捕获
│   ├── InputDialog                 # 输入对话框
│   └── DeleteConfirmDialog         # 删除确认对话框
│
├── Data/
│   └── AppDbContext.cs             # EF Core 数据库上下文
│
└── Docs/                           # 项目文档
    ├── 项目总结.md
    ├── 开发总结.md
    ├── 阶段性开发汇报与交接文档.md
    └── 录屏与实时增强技术总结.md
```

---

## 🚀 快速开始

### 环境要求

- **操作系统**: Windows 10 / 11 (x64)
- **运行环境**: [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- **开发环境**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) + Visual Studio 2022 或 VS Code

### 编译与运行

```bash
# 克隆仓库
git clone <your-repo-url>
cd MediaEnhancer

# 还原依赖并编译
dotnet restore
dotnet build

# 运行
dotnet run
```

首次运行时，FFmpeg 会在 `检查依赖` 时自动下载。也可以手动将 `ffmpeg.exe` 和 `ffprobe.exe` 放在编译输出目录。

### 使用步骤

1. **导入文件** — 点击"扫描文件夹"选择包含媒体文件的目录，或点击"导入文件"手动选择
2. **管理影音库** — 在"文件管理"页面浏览、筛选、收藏、重命名或删除文件
3. **增强画面** — 在"实时增强"页面加载图片预览增强效果，或右键文件选择"AI 增强"
4. **全屏增强** — 点击"启动全屏增强"，以透明覆盖窗口增强整个屏幕画面
5. **AI 对话** — 在"AI 对话"页面配置 API Key 后即可进行智能分析
6. **屏幕录制** — 在"屏幕录制"页面开始/停止录制，支持增强录制

---

## ⚙️ 配置说明

### AI API 配置

系统支持 OpenAI 兼容的 API 格式，预设后端的端点包括：

| 服务商 | 端点 | 需配置 |
|--------|------|--------|
| 阿里百炼 (DashScope) | `https://dashscope.aliyuncs.com/compatible-mode/v1` | API Key |
| OpenAI | `https://api.openai.com/v1` | API Key |
| DeepSeek | `https://api.deepseek.com` | API Key |
| 智谱 (BigModel) | `https://open.bigmodel.cn` | API Key |
| Ollama (本地) | `http://localhost:11434` | 无需密钥 |

在系统设置面板中填入 API Key 即可启用 AI 功能。未配置时，AI 对话会自动降级为本地模板分析。

### 路径配置

- **录屏目录**: 默认 `./Recordings/`
- **增强输出目录**: 默认 `./Enhancements/`
- **缩略图缓存目录**: 默认 `./Thumbnails/`

所有路径可在系统设置面板中自定义修改。

---

## 🗄️ 数据库设计

| 表名 | 说明 | 主要字段 |
|------|------|---------|
| `MediaFiles` | 媒体文件 | Title, FilePath(唯一), Type, FileFormat, FileSize, Width, Height, Duration, IsFavorite, Description, ThumbnailPath, DateAdded |
| `PlayHistories` | 播放记录 | MediaFileId(FK), PlayedAt, Progress |
| `EnhancementLogs` | 增强日志 | MediaFileId(FK), MethodName, OutputPath, CreatedAt |
| `Recordings` | 录屏记录 | MediaFileId(FK), FilePath, Duration, IsEnhanced |
| `Favorites` | 收藏记录 | MediaFileId(FK,唯一) |
| `Thumbnails` | 缩略图 | MediaFileId(FK,唯一), ThumbnailPath |

---

## 🎯 开发路线图

- [x] **阶段一**: 基础框架与影音管理（WPF 主界面、文件扫描导入、SQLite、筛选收藏）
- [x] **阶段二**: 线性拉伸增强（增强接口、实时全屏增强、离线文件增强、参数调节）
- [x] **阶段三**: AI 智能模块（大模型对话、图像生成、语音转文字、模板降级）
- [x] **阶段四**: 录屏与系统完善（录屏模块、全局热键、设置面板、依赖检查）
- [ ] **阶段五**: 增强方法扩展（直方图均衡化、CLAHE、MAXIM 深度学习模型）

---

## 📝 文档

- [项目需求说明书](./项目需求.md) — 完整的功能规格与技术架构说明
- [项目总结](./Docs/项目总结.md) — 项目概况、结构、功能完成度
- [开发总结](./Docs/开发总结.md) — 开发过程、技术细节与问题解决
- [录屏与实时增强技术总结](./Docs/录屏与实时增强技术总结.md) — DXGI、FFmpeg 编码等技术详解

---

## 👥 目标用户

- 有大量家庭录像、旅行视频需要整理和修复的普通用户
- 需要在低光、雾霾天气下查看监控画面的安防人员
- 经常观看在线低质量视频并希望实时提升画质的影音爱好者
- 视障用户群体，通过 AI 语音描述理解画面内容

---

## 📄 许可

本项目仅用于课程设计/学习目的。

---

## 🙏 致谢

本项目使用了以下开源项目：

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM 工具包
- [Entity Framework Core](https://github.com/dotnet/efcore) — ORM 框架
- [SQLite](https://www.sqlite.org/) — 嵌入式数据库
- [FFmpeg](https://ffmpeg.org/) — 视频编解码
- [NAudio](https://github.com/naudio/NAudio) — 音频处理库
- [TagLibSharp](https://github.com/mono/taglib-sharp) — 媒体元数据
- [Xabe.FFmpeg](https://github.com/tomaszzmuda/Xabe.FFmpeg) — FFmpeg .NET 封装
