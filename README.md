# 🎬 影音智增强系统

> 一站式本地影音资产管理 + AI 实时视觉增强 Windows 桌面应用

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-blue)](https://github.com/dotnet/wpf)
[![MVVM](https://img.shields.io/badge/Pattern-MVVM-green)](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
[![SQLite](https://img.shields.io/badge/DB-SQLite-orange)](https://www.sqlite.org/)
[![ONNX](https://img.shields.io/badge/ML-ONNX%20Runtime-lightgrey)](https://onnxruntime.ai/)

---

## 📖 项目简介

**影音智增强系统**是一款集**本地影音资产管理**与**AI 视觉增强**于一体的 Windows 桌面应用，旨在解决低光照、低对比度等低质量影像"看不清、理还乱"的痛点。

支持**线性拉伸 + Multinex Nano ONNX 深度学习模型**的可插拔增强架构，结合多模态大语言模型实现内容的智能理解与自动摘要，并配备多用户登录系统实现数据隔离。

---

## ✨ 核心功能

### 📁 影音管理
- **文件扫描与导入** — 递归扫描文件夹、多选导入，支持图片/视频/音频
- **元数据提取** — 自动获取类型、时长、分辨率、文件大小等信息
- **搜索筛选** — 关键词 + 类型 + 收藏 三维组合过滤
- **收藏管理** — 一键收藏/取消，支持批量操作，Favorites 表同步
- **播放记录** — 自动记录播放时间，提供"最近播放"快捷入口
- **文件校验** — 一键检查文件完整性，缺失文件可定位或删除

### 🔐 用户系统
- **注册 / 登录** — 独立账号，SHA256 + salt 密码存储
- **数据隔离** — 所有表按 `UserId` 过滤，文件/记录/设置互不可见
- **配置隔离** — 每个用户独立的 API Key 和路径配置

### 🎨 图像增强（可扩展插件架构）
- **线性拉伸** — 像素值线性映射到全动态范围，纯 C#，< 0.1ms/帧，可调对比度/亮度
- **Multinex Nano** — 超轻量 Retinex 网络（15K 参数），ONNX Runtime CPU 推理
- 统一 `IRealTimeEnhancer` + `IOnnxEnhancement` 接口，新算法一行注册

### 🖥️ 实时增强（全屏覆盖）
- 方法仅限支持实时的轻量算法：线性拉伸 + Multinex Nano
- DXGI Desktop Duplication（GPU 零拷贝）+ GDI 自动回退
- 鼠标穿透（`WS_EX_TRANSPARENT`），F11 全局热键退出
- 独立方法选择下拉框，全屏按钮在离线方法选中时自动禁用

### 🔧 离线增强（文件 / 视频）
- 独立页面设置离线增强方法，可选全部方法
- 详情面板一键增强
- 视频逐帧增强（FFmpeg 解帧 → 逐帧增强 → 合帧 + 音轨），支持取消
- 增强文件自动入库，关联源文件（`SourceFileId`），参数持久化至日志

### 💬 AI 对话
- OpenAI 兼容 API（通义千问 / DeepSeek / Ollama 等）
- 多模态：图片 base64 嵌入，视频 FFmpeg 抽关键帧
- 快捷预设：AI 简介、数据摘要
- 未配置 API 时自动降级为本地模板分析

### 🎨 AI 图像编辑
- 文生图 / 图生图，支持通义万相 / SiliconFlow / OpenAI 兼容 API
- 生成结果可保存并自动入库

### 🎥 屏幕录制
- DXGI + GDI 双模式捕获，JPEG 帧 + FFmpeg 编码
- 编码器逐级降级：h264_nvenc → h264_qsv → h264_amf → libx264 → mpeg4
- 增强录制：**录制时不增强（保证帧率），停止后后处理逐帧增强再编码**
- 后处理增强可中途取消，直接使用原始帧编码
- 录制文件自动入库，录制历史列表

### 📊 数据统计
- 九宫格仪表盘：文件总数、图片、视频、音频、增强次数、实时增强、录屏次数、总播放、收藏数
- 依赖检查（自动下载 FFmpeg）、缓存清理、文件校验

---

## 🏗️ 技术架构

| 分类 | 技术 |
|------|------|
| 桌面框架 | .NET 10 WPF |
| 架构模式 | MVVM (CommunityToolkit.Mvvm 8.4) |
| 数据库 | SQLite + Entity Framework Core 10 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection |
| 屏幕捕获 | DXGI Desktop Duplication / GDI |
| 视频处理 | FFmpeg (Xabe.FFmpeg) |
| 深度学习 | ONNX Runtime (Microsoft.ML.OnnxRuntime) |
| 元数据 | TagLibSharp |
| AI 集成 | OpenAI 兼容 API（通义千问 / DeepSeek / Ollama） |
| 图像生成 | 通义万相 / SiliconFlow / OpenAI 兼容 |
| API 密钥保护 | Windows DPAPI 加密存储 |

---

## 📁 项目结构

```
MediaEnhancer/
├── App.xaml / App.xaml.cs              # 应用入口、DI 容器、登录流程
├── MainWindow.xaml / .cs               # 主窗口（8 页面）
│
├── Models/                             # 数据实体（8 个）
│   ├── MediaFile.cs                    # 媒体文件
│   ├── PlayHistory.cs                  # 播放记录
│   ├── EnhancementLog.cs               # 增强日志
│   ├── Recording.cs                    # 录屏记录
│   ├── Favorite.cs                     # 收藏记录
│   ├── RealtimeSession.cs              # 实时增强会话
│   ├── User.cs                         # 用户
│   └── ChatMessage.cs                  # AI 对话消息
│
├── Core/                               # 核心组件
│   ├── IEnhancementMethod.cs           # 增强方法根接口
│   ├── IRealTimeEnhancer.cs            # 实时逐帧增强接口
│   ├── INativeEnhancement.cs           # 离线增强接口
│   ├── IOnnxEnhancement.cs             # ONNX 推理接口
│   ├── LinearStretchMethod.cs          # 线性拉伸（纯 C#）
│   ├── MultinexNanoMethod.cs           # Multinex Nano ONNX 增强
│   ├── OnnxModelHelper.cs              # ONNX 预处理/后处理/缩放
│   ├── EnhancementRegistry.cs          # 增强方法注册中心
│   ├── MediaFileUtils.cs               # 媒体文件工具类
│   └── *Converter.cs                   # WPF 值转换器
│
├── Services/                           # 业务服务（15 个文件）
│   ├── DataService.cs / IDataService.cs
│   ├── AuthService.cs                  # 用户认证（含接口）
│   ├── AiService.cs                    # AI 对话/图像生成
│   ├── ScreenRecorder.cs              # 屏幕录制器（后处理增强）
│   ├── VideoEnhancer.cs               # 视频增强器
│   ├── ThumbnailService.cs            # 缩略图服务
│   ├── FileScanService.cs             # 文件扫描
│   ├── PlaybackService.cs             # 播放服务
│   ├── AppConfig.cs                    # 配置持久化（按用户隔离）
│   └── SecureStorage.cs               # DPAPI 加密存储
│
├── ViewModels/                         # MVVM 视图模型（6 个文件）
│   ├── MainViewModel.cs                # 主 VM + 文件管理 + 批量操作
│   ├── MainViewModel.Enhancement.cs    # 实时/离线增强 + 录屏
│   ├── MainViewModel.Ai.cs            # AI 对话/编辑逻辑
│   ├── MainViewModel.Dashboard.cs      # 仪表盘统计
│   ├── MainViewModel.Settings.cs       # 系统设置
│   └── LoginViewModel.cs              # 登录 VM
│
├── Views/                              # 窗口与控件（15 个文件）
│   ├── FullscreenEnhanceWindow         # 全屏增强覆盖窗口
│   ├── MediaPlayerWindow               # 媒体播放器
│   ├── ImageViewerWindow               # 图片查看器
│   ├── LoginWindow                     # 登录/注册窗口
│   ├── DxgiScreenCapture.cs            # DXGI 屏幕捕获（纯 P/Invoke）
│   └── ...                             # 对话框、详情面板等
│
├── Data/
│   ├── AppDbContext.cs                 # EF Core 数据库上下文
│   └── AppDbContextFactory.cs          # 设计时工厂
│
├── Migrations/                         # EF Core 数据库迁移
├── OnnxModels/                         # ONNX 模型文件（1 个）
│   └── multinex_nano_lolv2_syn.onnx
├── Docs/                               # 项目文档
│
├── .gitignore                          # Git 忽略规则
├── setup.iss                           # Inno Setup 安装脚本
├── README.md                           # 本文件
└── 项目需求.md                          # 课程设计需求
```

---

## 🚀 快速开始

### 环境要求

- **操作系统**: Windows 10 / 11 (x64)
- **开发环境**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) + Visual Studio 2022 或 VS Code
- **运行环境**: 自包含发布无需安装 .NET

### 编译与运行

```bash
git clone https://github.com/WTXwang/MediaEnhancer.git
cd MediaEnhancer

dotnet restore
dotnet build
dotnet run
```

### 首次运行

1. **注册账号** → 首次启动弹出登录窗，点击"注册"创建账号
2. **下载 FFmpeg** → 进入"数据统计"页 → 点击"检查依赖"
3. **导入文件** → "文件管理"页 → 选择文件夹或导入文件
4. **配置 AI（可选）** → "系统设置"页 → 填入 API Key

### 发布

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:UseAppHost=true
```

然后打开 Inno Setup 编译 `setup.iss` 生成安装包。

---

## 🗄️ 数据库设计

| 表 | 说明 | 主要字段 |
|------|------|---------|
| `Users` | 用户 | Username(唯一), PasswordHash, Salt |
| `MediaFiles` | 媒体文件 | FilePath+UserId(复合唯一), Type, FileFormat, FileSize, Width, Height, Duration, IsFavorite, SourceFileId, ThumbnailPath |
| `PlayHistories` | 播放记录 | MediaFileId(FK), UserId, PlayedAt |
| `EnhancementLogs` | 增强日志 | MediaFileId(FK), UserId, MethodName, OutputPath, ParametersJson |
| `Recordings` | 录屏记录 | MediaFileId(FK,唯一), UserId, Title, FilePath, Duration, FileSize, IsEnhanced, AudioSource |
| `Favorites` | 收藏记录 | MediaFileId(FK,唯一), UserId, CreatedAt |
| `RealtimeSessions` | 实时增强会话 | UserId, MethodName, StartedAt, StoppedAt, DurationSeconds |

---

## 🎯 开发路线图

- [x] **阶段一**: 基础框架与影音管理
- [x] **阶段二**: 线性拉伸增强 + 全屏实时增强
- [x] **阶段三**: AI 对话 + AI 图像编辑
- [x] **阶段四**: 录屏 + 用户系统 + 设置持久化
- [x] **阶段五**: ONNX 深度学习模型集成（Multinex Nano）

---

## 🙏 致谢

- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [Entity Framework Core](https://github.com/dotnet/efcore)
- [SQLite](https://www.sqlite.org/)
- [FFmpeg](https://ffmpeg.org/)
- [ONNX Runtime](https://onnxruntime.ai/)
- [NAudio](https://github.com/naudio/NAudio)
- [TagLibSharp](https://github.com/mono/taglib-sharp)
- [Inno Setup](https://jrsoftware.org/isinfo.php)

---

## 📄 许可

本项目仅用于课程设计/学习目的。
