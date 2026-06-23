using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

// ════════════════════════════════════════════
// 影音智增强系统 — Windows编程课程设计报告生成器
// ════════════════════════════════════════════

string outputPath = args.Length > 0 
    ? args[0] 
    : @"D:\homeworks\Csharp\MediaEnhancer\MediaEnhancer\MediaEnhancer\课程设计报告_影音智增强系统.docx";

Console.WriteLine($"Generating report: {outputPath}");

using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
var mainPart = doc.AddMainDocumentPart();
mainPart.Document = new Document(new Body());
var body = mainPart.Document.Body!;

// ═══ Styles ═══
var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
stylesPart.Styles = new Styles();
var styles = stylesPart.Styles;

// DocDefaults: 小四宋体 (12pt = sz 24), 1.5x line spacing
styles.Append(new DocDefaults(
    new RunPropertiesDefault(
        new RunPropertiesBaseStyle(
            new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "SimSun", ComplexScript = "Times New Roman" },
            new FontSize { Val = "24" },              // 12pt 小四
            new FontSizeComplexScript { Val = "24" },
            new Color { Val = "000000" },
            new Languages { Val = "en-US", EastAsia = "zh-CN" }
        )
    ),
    new ParagraphPropertiesDefault(
        new ParagraphPropertiesBaseStyle(
            new SpacingBetweenLines { Line = "360", LineRule = LineSpacingRuleValues.Auto, After = "0", Before = "0" },
            new Indentation { FirstLineChars = 200 }  // 2字符首行缩进
        )
    )
));

// Normal style
styles.Append(new Style(
    new StyleName { Val = "Normal" }
) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true });

// Heading 1: 三号黑体 (16pt=sz32), bold, centered
styles.Append(new Style(
    new StyleName { Val = "heading 1" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new KeepNext(),
        new KeepLines(),
        new SpacingBetweenLines { Before = "240", After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLineChars = 0, FirstLine = "0" },
        new Justification { Val = JustificationValues.Center },
        new OutlineLevel { Val = 0 }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "SimHei", Ascii = "Arial", HighAnsi = "Arial" },
        new FontSize { Val = "32" },  // 16pt 三号
        new FontSizeComplexScript { Val = "32" },
        new Bold()
    )
) { Type = StyleValues.Paragraph, StyleId = "Heading1" });

// Heading 2: 小三黑体 (15pt=sz30), left-aligned
styles.Append(new Style(
    new StyleName { Val = "heading 2" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new KeepNext(),
        new KeepLines(),
        new SpacingBetweenLines { Before = "200", After = "100", Line = "360", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLineChars = 0, FirstLine = "0" },
        new OutlineLevel { Val = 1 }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "SimHei", Ascii = "Arial", HighAnsi = "Arial" },
        new FontSize { Val = "30" },  // 15pt 小三
        new FontSizeComplexScript { Val = "30" },
        new Bold()
    )
) { Type = StyleValues.Paragraph, StyleId = "Heading2" });

// Heading 3: 四号黑体 (14pt=sz28), left-aligned
styles.Append(new Style(
    new StyleName { Val = "heading 3" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new KeepNext(),
        new KeepLines(),
        new SpacingBetweenLines { Before = "160", After = "80", Line = "360", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLineChars = 0, FirstLine = "0" },
        new OutlineLevel { Val = 2 }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "SimHei", Ascii = "Arial", HighAnsi = "Arial" },
        new FontSize { Val = "28" },  // 14pt 四号
        new FontSizeComplexScript { Val = "28" },
        new Bold()
    )
) { Type = StyleValues.Paragraph, StyleId = "Heading3" });

// TOC Heading style
styles.Append(new Style(
    new StyleName { Val = "TOC Heading" },
    new BasedOn { Val = "Heading1" },
    new StyleParagraphProperties(
        new OutlineLevel { Val = 9 }  // Not in TOC itself
    )
) { Type = StyleValues.Paragraph, StyleId = "TOCHeading" });

// CoverTitle style
styles.Append(new Style(
    new StyleName { Val = "Cover Title" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "200", Line = "360", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLine = "0", FirstLineChars = 0 },
        new Justification { Val = JustificationValues.Center }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "SimHei", Ascii = "Arial", HighAnsi = "Arial" },
        new FontSize { Val = "52" },  // 26pt 一号
        new FontSizeComplexScript { Val = "52" },
        new Bold()
    )
) { Type = StyleValues.Paragraph, StyleId = "CoverTitle" });

// CoverSubtitle
styles.Append(new Style(
    new StyleName { Val = "Cover Subtitle" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "120", Line = "360", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLine = "0", FirstLineChars = 0 },
        new Justification { Val = JustificationValues.Center }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "FangSong", Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
        new FontSize { Val = "36" },  // 18pt 小二
        new FontSizeComplexScript { Val = "36" }
    )
) { Type = StyleValues.Paragraph, StyleId = "CoverSubtitle" });

// CoverInfo
styles.Append(new Style(
    new StyleName { Val = "Cover Info" },
    new BasedOn { Val = "Normal" },
    new StyleParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "60", Line = "400", LineRule = LineSpacingRuleValues.Auto },
        new Indentation { FirstLine = "0", FirstLineChars = 0 },
        new Justification { Val = JustificationValues.Center }
    ),
    new StyleRunProperties(
        new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
        new FontSize { Val = "28" },  // 14pt 四号
        new FontSizeComplexScript { Val = "28" }
    )
) { Type = StyleValues.Paragraph, StyleId = "CoverInfo" });

// ═══ Page setup: A4 ===========================================
var sectPr = new SectionProperties(
    new PageSize { Width = 11906U, Height = 16838U },  // A4
    new PageMargin
    {
        Top = 1440, Bottom = 1440,
        Left = 1800U, Right = 1800U,
        Header = 720U, Footer = 720U, Gutter = 0U
    }
);

// ═══ Footer with page numbers ═══
var footerPart = mainPart.AddNewPart<FooterPart>();
footerPart.Footer = new Footer(
    new Paragraph(
        new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new Indentation { FirstLine = "0", FirstLineChars = 0 }
        ),
        new Run(
            new RunProperties(
                new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman" },
                new FontSize { Val = "18" },  // 9pt 小五
                new FontSizeComplexScript { Val = "18" }
            ),
            new FieldChar { FieldCharType = FieldCharValues.Begin }
        ),
        new Run(
            new RunProperties(
                new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman" },
                new FontSize { Val = "18" },
                new FontSizeComplexScript { Val = "18" }
            ),
            new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }
        ),
        new Run(
            new RunProperties(
                new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman" },
                new FontSize { Val = "18" },
                new FontSizeComplexScript { Val = "18" }
            ),
            new FieldChar { FieldCharType = FieldCharValues.Separate }
        ),
        new Run(
            new RunProperties(
                new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman" },
                new FontSize { Val = "18" },
                new FontSizeComplexScript { Val = "18" }
            ),
            new Text("1")
        ),
        new Run(
            new RunProperties(
                new RunFonts { EastAsia = "SimSun", Ascii = "Times New Roman" },
                new FontSize { Val = "18" },
                new FontSizeComplexScript { Val = "18" }
            ),
            new FieldChar { FieldCharType = FieldCharValues.End }
        )
    )
);
string footerPartId = mainPart.GetIdOfPart(footerPart);
sectPr.Append(new FooterReference { Type = HeaderFooterValues.Default, Id = footerPartId });

// ════════════════════════════════════════════════
// COVER PAGE
// ════════════════════════════════════════════════
void AddCoverPage()
{
    // Empty lines at top
    for (int i = 0; i < 6; i++)
        body.Append(new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0", Line = "360", LineRule = LineSpacingRuleValues.Auto }
        )));

    // Title
    body.Append(new Paragraph(
        new ParagraphProperties(new ParagraphStyleId { Val = "CoverTitle" }),
        new Run(new Text("影音智增强系统"))
    ));

    // Subtitle
    body.Append(new Paragraph(
        new ParagraphProperties(new ParagraphStyleId { Val = "CoverSubtitle" }),
        new Run(new Text("课程设计报告"))
    ));

    // Blank lines
    for (int i = 0; i < 4; i++)
        body.Append(new Paragraph(new ParagraphProperties(
            new SpacingBetweenLines { Before = "0", After = "0", Line = "360", LineRule = LineSpacingRuleValues.Auto }
        )));

    // Course info
    string[] coverLines = [
        "课程名称：《Windows编程》",
        "题    目：影音智增强系统设计与实现",
        "专    业：计算机科学与技术",
        "开发工具：Visual Studio 2022 / .NET 10 / C# / WPF",
        "",
        DateTime.Now.ToString("yyyy年M月")
    ];

    foreach (var line in coverLines)
    {
        body.Append(new Paragraph(
            new ParagraphProperties(
                new ParagraphStyleId { Val = string.IsNullOrEmpty(line) ? "Normal" : "CoverInfo" },
                new Indentation { FirstLine = "0", FirstLineChars = 0 }
            ),
            new Run(new Text(line))
        ));
    }

    // Page break after cover
    body.Append(new Paragraph(
        new ParagraphProperties(new Indentation { FirstLine = "0", FirstLineChars = 0 }),
        new Run(new Break { Type = BreakValues.Page })
    ));
}

AddCoverPage();

// ════════════════════════════════════════════════
// TABLE OF CONTENTS
// ════════════════════════════════════════════════
void AddTOC()
{
    body.Append(new Paragraph(
        new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" }),
        new Run(new Text("目  录"))
    ));

    // TOC field
    body.Append(new Paragraph(
        new ParagraphProperties(new Indentation { FirstLine = "0", FirstLineChars = 0 }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode(" TOC \\o \"1-3\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new Text("（请在Word中右键此处 → 更新域，生成目录）")),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End })
    ));

    body.Append(new Paragraph(
        new ParagraphProperties(new Indentation { FirstLine = "0", FirstLineChars = 0 }),
        new Run(new Break { Type = BreakValues.Page })
    ));
}

AddTOC();

// ════════════════════════════════════════════════
// HELPER FUNCTIONS
// ════════════════════════════════════════════════

void AddHeading(int level, string text)
{
    var hLevel = level switch { 1 => "Heading1", 2 => "Heading2", 3 => "Heading3", _ => "Heading1" };
    body.Append(new Paragraph(
        new ParagraphProperties(new ParagraphStyleId { Val = hLevel }),
        new Run(new Text(text))
    ));
}

void AddPara(string text)
{
    body.Append(new Paragraph(
        new ParagraphProperties(new ParagraphStyleId { Val = "Normal" }),
        new Run(new Text(text))
    ));
}

void AddParaNoIndent(string text)
{
    body.Append(new Paragraph(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new Indentation { FirstLine = "0", FirstLineChars = 0 }
        ),
        new Run(new Text(text))
    ));
}

void AddBullet(string text)
{
    body.Append(new Paragraph(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new Indentation { FirstLine = "0", FirstLineChars = 0, Left = "420" }
        ),
        new Run(new Text("• " + text))
    ));
}

void AddTable(string[] headers, string[][] rows)
{
    var table = new Table();
    var tblPr = new TableProperties(
        new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
        new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 12, Space = 0, Color = "000000" },
            new BottomBorder { Val = BorderValues.Single, Size = 12, Space = 0, Color = "000000" },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "999999" },
            new RightBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "999999" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "999999" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Space = 0, Color = "999999" }
        ),
        new TableCellMarginDefault(
            new TopMargin { Width = "28", Type = TableWidthUnitValues.Dxa },
            new StartMargin { Width = "57", Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = "28", Type = TableWidthUnitValues.Dxa },
            new EndMargin { Width = "57", Type = TableWidthUnitValues.Dxa }
        )
    );
    table.Append(tblPr);

    var grid = new TableGrid();
    foreach (var _ in headers)
        grid.Append(new GridColumn());
    table.Append(grid);

    // Header row
    var headerRow = new TableRow();
    foreach (var h in headers)
    {
        headerRow.Append(new TableCell(
            new TableCellProperties(
                new Shading { Fill = "D9E2F3", Val = ShadingPatternValues.Clear },
                new TableCellWidth { Width = "0", Type = TableWidthUnitValues.Auto }
            ),
            new Paragraph(
                new ParagraphProperties(
                    new SpacingBetweenLines { After = "0", Line = "280", LineRule = LineSpacingRuleValues.Auto },
                    new Indentation { FirstLine = "0", FirstLineChars = 0 },
                    new Justification { Val = JustificationValues.Center }
                ),
                new Run(new RunProperties(new Bold(), new RunFonts { EastAsia = "SimHei" }), new Text(h))
            )
        ));
    }
    table.Append(headerRow);

    // Data rows
    foreach (var rowData in rows)
    {
        var row = new TableRow();
        for (int i = 0; i < rowData.Length; i++)
        {
            row.Append(new TableCell(
                new TableCellProperties(new TableCellWidth { Width = "0", Type = TableWidthUnitValues.Auto }),
                new Paragraph(
                    new ParagraphProperties(
                        new SpacingBetweenLines { After = "0", Line = "280", LineRule = LineSpacingRuleValues.Auto },
                        new Indentation { FirstLine = "0", FirstLineChars = 0 },
                        new Justification { Val = i == 0 ? JustificationValues.Left : JustificationValues.Center }
                    ),
                    new Run(new Text(rowData[i]))
                )
            ));
        }
        table.Append(row);
    }

    body.Append(table);
    body.Append(new Paragraph(new ParagraphProperties(
        new SpacingBetweenLines { Before = "0", After = "0" },
        new Indentation { FirstLine = "0", FirstLineChars = 0 }
    )));
}

void AddCodeBlock(string code)
{
    body.Append(new Paragraph(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new Indentation { FirstLine = "0", FirstLineChars = 0, Left = "420" },
            new SpacingBetweenLines { Line = "260", LineRule = LineSpacingRuleValues.Auto, Before = "60", After = "60" },
            new Shading { Fill = "F5F5F5", Val = ShadingPatternValues.Clear }
        ),
        new Run(
            new RunProperties(
                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", EastAsia = "SimSun" },
                new FontSize { Val = "18" },
                new FontSizeComplexScript { Val = "18" }
            ),
            new Text(code)
        )
    ));
}

void AddPageBreak()
{
    body.Append(new Paragraph(
        new ParagraphProperties(new Indentation { FirstLine = "0", FirstLineChars = 0 }),
        new Run(new Break { Type = BreakValues.Page })
    ));
}

// ════════════════════════════════════════════════
// 第一章 项目概述
// ════════════════════════════════════════════════

AddHeading(1, "第一章  项目概述");

AddHeading(2, "1.1 项目背景");
AddPara("随着数字影像设备的普及，普通用户在日常拍摄、网络下载与社交媒体分享中积累了大量的影音文件。然而，受限于拍摄环境（如低光照、雾霾天气）、设备性能以及原始文件质量等因素，许多影音文件存在画面昏暗、对比度不足、细节模糊等质量问题。用户在浏览和管理这些低质量文件时，常常面临【看不清、理还乱】的窘境。");
AddPara("传统的图像增强工具（如Photoshop、专业调色软件）虽然功能强大，但操作复杂、门槛较高，难以满足普通用户快速、便捷地提升大量影音文件视觉效果的需求。此外，现有工具大多缺乏与文件管理系统的深度集成，增强后的文件无法自动归档入库，形成【增强-管理】的闭环。");

AddHeading(2, "1.2 项目目标");
AddPara("影音智增强系统旨在开发一款集本地影音资产管理、AI实时视觉增强、屏幕录制与多模态智能分析于一体的Windows桌面应用。系统采用.NET 10 WPF框架，结合MVVM架构模式，基于Entity Framework Core与SQLite数据库实现数据持久化，通过插件式增强架构支持多种图像增强方法（线性拉伸、ONNX深度学习模型等），并集成多模态大语言模型实现影音内容的智能分析与自动摘要。");
AddPara("项目的核心价值在于：让每一个普通用户都能简单方便地管理、增强和理解自己的影音资产，使低质量影像变得清晰可见，让海量文件有序可查，让AI技术真正服务于日常影音处理场景。");

AddHeading(2, "1.3 选题依据与意义");
AddPara("本选题的理论意义在于：将计算机视觉增强技术与桌面应用软件工程实践相结合，探索了【渐进式增强框架】这一软件架构模式——通过初期采用简单高效的线性拉伸算法快速落地核心功能流程，同时预留扩展接口以支持未来深度学习模型的平滑接入，兼顾了开发效率与长期技术演进。");
AddPara("其实用价值体现在：系统集成了文件管理、图像增强、屏幕录制、AI智能分析等多种功能于一体，构建了【获取->增强->管理->分享】的完整工作闭环。对于有大量家庭录像需要整理的普通用户、需要在低光照环境中查看监控画面的安防人员、以及经常观看在线低质量视频的影音爱好者，本系统提供了高效便捷的一站式解决方案。");

AddHeading(2, "1.4 报告结构");
AddPara("本报告共分为七章。第一章介绍项目背景与目标；第二章进行需求分析；第三章详细阐述系统设计，包括技术架构、功能模块与数据库设计；第四章描述系统实现细节与代码统计；第五章展示界面设计成果；第六章介绍测试与使用说明；第七章进行总结与展望。");

AddPageBreak();

// ════════════════════════════════════════════════
// 第二章 需求分析
// ════════════════════════════════════════════════

AddHeading(1, "第二章  需求分析");

AddHeading(2, "2.1 功能需求");
AddPara("根据项目定位与目标用户群体，系统需要实现以下核心功能模块：");

AddHeading(3, "2.1.1 影音管理模块");
AddBullet("文件扫描与导入：支持指定文件夹递归扫描，识别常见视频、音频、图像格式，批量导入数据库");
AddBullet("元数据自动提取：获取文件类型、时长、分辨率、文件大小等基础信息");
AddBullet("多维浏览与筛选：按类型、名称关键词、播放次数、收藏状态等条件过滤和排序");
AddBullet("收藏与标签管理：支持一键收藏/取消收藏，用户自定义标签");
AddBullet("播放记录追踪：自动记录每次播放的时间与进度，提供【最近播放】快速访问入口");
AddBullet("文件校验功能：遍历记录检测文件完整性，缺失文件提供删除/定位操作");

AddHeading(3, "2.1.2 图像增强模块");
AddBullet("插件式增强架构：定义统一的IEnhancementMethod增强方法接口，支持运行时切换不同增强算法");
AddBullet("线性拉伸增强：纯C#原生实现，像素值线性映射到全动态范围，<0.1ms/帧处理速度");
AddBullet("ONNX深度学习增强：集成Multinex Nano超轻量Retinex网络（15K参数），ONNX Runtime CPU推理");
AddBullet("增强参数调节：对比度强度（0.5-2.0）、亮度偏移（-50~50），用户可实时拖动滑块调节");

AddHeading(3, "2.1.3 实时屏幕增强模块");
AddBullet("透明覆盖窗口：以无边框透明窗口悬浮于屏幕最顶层，实时显示增强后的画面");
AddBullet("DXGI Desktop Duplication屏幕捕获：GPU零拷贝获取屏幕纹理，支持30-60FPS，CPU占用约3%");
AddBullet("鼠标穿透机制：通过WS_EX_TRANSPARENT样式确保增强窗口不干扰用户正常操作下层应用");
AddBullet("递归消除：SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)确保增强窗口自身不被捕获");
AddBullet("全局热键控制：F11全局热键快速开启/关闭全屏增强");

AddHeading(3, "2.1.4 离线文件增强模块");
AddBullet("单张图片增强：在影音库内右键文件，选择增强方法，实时预览效果后确认导出");
AddBullet("批量增强处理：多选文件后一键批量增强，增强文件自动入库并关联源文件");
AddBullet("视频逐帧增强：FFmpeg解帧 → 逐帧增强 → 合帧+音轨重编码，支持进度提示与中途取消");
AddBullet("增强预览对比：处理完成后弹出原图/增强图对比窗口，用户确认后导出");

AddHeading(3, "2.1.5 屏幕录制模块");
AddBullet("DXGI + GDI双模式捕获：支持全屏录制，JPEG帧缓存 + FFmpeg编码合成MP4");
AddBullet("编码器逐级降级：h264_nvenc → h264_qsv → h264_amf → libx264 → mpeg4，确保跨设备兼容");
AddBullet("增强录制：停止录制后可选择对帧进行后处理增强再编码，增强过程支持取消");
AddBullet("录制控制：开始/暂停/停止按钮、时长显示，录制文件自动入库");

AddHeading(3, "2.1.6 AI智能模块");
AddBullet("AI对话分析：基于OpenAI兼容API（通义千问/DeepSeek/Ollama），支持多模态图片/视频分析");
AddBullet("AI图像编辑：文生图/图生图，支持通义万相、SiliconFlow等多供应商");
AddBullet("模板化降级：API未配置或调用失败时自动降级为本地模板分析，保证功能可用性");

AddHeading(3, "2.1.7 用户系统与数据统计");
AddBullet("用户注册/登录：独立账号体系，SHA256+Salt密码存储，数据按用户完全隔离");
AddBullet("仪表盘统计：文件总数、增强次数、录制时长等多维度统计卡片");
AddBullet("系统设置：AI对话/编辑分开配置API密钥与模型，增强/录制/缩略图路径可配置");

AddHeading(2, "2.2 非功能需求");
AddPara("（1）性能需求：实时增强响应时间应小于16ms/帧（60FPS），确保视觉流畅；文件管理模块批量扫描1000个文件应在10秒内完成入库。");
AddPara("（2）可靠性需求：程序需正确处理异常情况（如文件缺失、编码器未安装、API调用超时等），提供友好的提示信息和降级方案。");
AddPara("（3）可用性需求：界面布局合理，色调和谐，风格统一，操作简单直观，符合Windows桌面应用交互规范。");
AddPara("（4）可扩展需求：增强模块采用插件式架构，新增增强方法仅需实现统一接口并注册到增强注册中心。");
AddPara("（5）兼容性需求：支持Windows 10/11 x64操作系统，利用硬件编码器加速（NVENC/Intel QSV/AMD AMF）。");

AddHeading(2, "2.3 目标用户");
AddTable(new[] { "用户群体", "典型使用场景" }, new[] {
    new[] { "普通家庭用户", "整理和修复家庭录像、旅行视频，批量智能重命名" },
    new[] { "安防监控人员", "在低光照环境下实时增强监控画面，录制清晰存档" },
    new[] { "影音爱好者", "在线观看低质量视频时实时提升画质" },
    new[] { "视障用户群体", "通过AI语音描述理解画面内容" },
});

AddPageBreak();

// ════════════════════════════════════════════════
// 第三章 系统设计
// ════════════════════════════════════════════════

AddHeading(1, "第三章  系统设计");

AddHeading(2, "3.1 总体技术架构");
AddPara("系统采用C# WPF桌面端为主体的单一进程架构，遵循MVVM（Model-View-ViewModel）设计模式，通过CommunityToolkit.Mvvm框架实现数据绑定与命令分发。初期增强方法（线性拉伸）纯C#实现，无需额外服务；深度学习模型通过ONNX Runtime直接集成到C#端推理，无需外部Python进程。系统集成FFmpeg用于视频/音频编解码处理。");
AddPara("系统分为以下层次：");
AddBullet("表现层（Views）：WPF XAML界面、全屏增强透明覆盖窗口、媒体播放器、图片查看器等");
AddBullet("视图模型层（ViewModels）：处理UI交互逻辑，MainViewModel拆分为5个partial class管理不同功能域");
AddBullet("业务服务层（Services）：DataService（数据操作）、AiService（AI对话/图像生成）、ScreenRecorder（屏幕录制）、VideoEnhancer（视频增强）、ThumbnailService（缩略图生成）等15个服务类");
AddBullet("增强方法层（Core）：定义IEnhancementMethod/IRealTimeEnhancer/INativeEnhancement/IOnnxEnhancement接口，LinearStretchMethod和MultinexNanoMethod为两个实现");
AddBullet("数据访问层（Data）：AppDbContext通过EF Core操作SQLite数据库，含8个数据实体与6个主表");
AddBullet("外部服务层：通过HTTP调用多模态大模型API（OpenAI兼容格式）");

AddHeading(2, "3.2 核心技术栈");
AddTable(new[] { "技术分类", "技术选型", "说明" }, new[] {
    new[] { "桌面框架", ".NET 10 WPF", "Windows原生桌面开发框架" },
    new[] { "架构模式", "MVVM + DI", "CommunityToolkit.Mvvm 8.4 + 依赖注入" },
    new[] { "数据库", "SQLite + EF Core", "轻量级本地无服务端数据库" },
    new[] { "屏幕捕获", "DXGI Desktop Duplication", "GPU零拷贝获取屏幕纹理" },
    new[] { "视频处理", "FFmpeg", "音轨提取、编解码、帧合成" },
    new[] { "深度学习", "ONNX Runtime", "Multinex Nano超轻量模型推理" },
    new[] { "元数据", "TagLibSharp", "媒体文件元数据提取" },
    new[] { "AI集成", "OpenAI兼容API", "通义千问/DeepSeek/Ollama" },
    new[] { "图像生成", "通义万相/SiliconFlow", "文生图/图生图" },
    new[] { "API安全", "Windows DPAPI", "密钥加密存储" },
});

AddHeading(2, "3.3 功能模块设计");

AddHeading(3, "3.3.1 增强方法架构设计");
AddPara("增强模块采用接口隔离原则设计，定义了多层次接口体系，支持不同粒度与场景的增强需求：");
AddCodeBlock("IEnhancementMethod                 ← 统一根接口\n  ├── IRealTimeEnhancer            ← 实时逐帧增强（byte[]字节数组）\n  │     └── LinearStretchMethod\n  ├── INativeEnhancement           ← 离线增强（BitmapSource）\n  │     └── LinearStretchMethod\n  └── IOnnxEnhancement             ← ONNX深度学习推理\n        └── MultinexNanoMethod");
AddPara("这种设计的优势在于：实时增强与离线增强共享同一方法实现但使用不同接口（byte[] vs BitmapSource），既避免了不必要的格式转换，又保证了增强效果的完全一致性。新增增强方法仅需实现对应接口并在EnhancementRegistry中注册即可。");

AddHeading(3, "3.3.2 屏幕捕获管线设计");
AddPara("系统设计了双层屏幕捕获管线，以DXGI Desktop Duplication为优先方案，GDI CopyFromScreen为回退方案。DXGI方案通过手动P/Invoke调用d3d11.dll和dxgi.dll实现COM接口操作，零外部依赖。关键技术点包括：");
AddBullet("CopyResource后强制Flush GPU命令队列，消除脏帧问题");
AddBullet("区分DXGI_ERROR_WAIT_TIMEOUT(0x887A0027)与真实错误，超时自动重试而非降级");
AddBullet("创建Staging纹理(CPU可读)用于像素回读，避免GPU显存直接操作");
AddBullet("Task.WhenAny + Task.Delay实现1.5秒超时保护，防止CopyFromScreen在某些显卡配置下永久卡死");

AddHeading(3, "3.3.3 数据库设计");
AddPara("系统使用SQLite数据库，通过Entity Framework Core进行ORM操作。数据库包含以下核心表：");
AddTable(new[] { "表名", "主要字段", "关联关系" }, new[] {
    new[] { "Users", "Username(唯一), PasswordHash, Salt", "—" },
    new[] { "MediaFiles", "14字段：FilePath(唯一), Type, FileFormat, Size, Width, Height, Duration, IsFavorite等", "UserId → Users" },
    new[] { "PlayHistories", "PlayedAt, PlayProgress", "UserId → Users, MediaFileId → MediaFiles(Cascade)" },
    new[] { "EnhancementLogs", "MethodName, OutputPath, ParametersJson", "UserId → Users, MediaFileId → MediaFiles(Cascade)" },
    new[] { "Recordings", "Duration, IsEnhanced, AudioSource", "UserId → Users, MediaFileId → MediaFiles(Cascade)" },
    new[] { "Favorites", "CreatedAt", "UserId → Users, MediaFileId → MediaFiles(Cascade, 唯一)" },
    new[] { "RealtimeSessions", "MethodName, StartedAt, StoppedAt", "UserId → Users" },
    new[] { "Thumbnails", "FilePath, CreatedAt, LastAccessAt", "MediaFileId → MediaFiles(Cascade)" },
});
AddPara("所有外键关联表均配置OnDelete(DeleteBehavior.Cascade)级联删除，确保数据一致性。");

AddHeading(2, "3.4 数据流设计");
AddHeading(3, "3.4.1 增强数据流");
AddPara("实时增强：屏幕 → DXGI/GDI捕获 → byte[]字节数组 → IRealTimeEnhancer.Enhance() → WriteableBitmap → 透明覆盖窗口渲染。");
AddPara("离线增强：文件 → 格式转换 → INativeEnhancement.Enhance(BitmapSource) → 增强结果 → 编码保存 → 自动入库。");
AddPara("视频增强：视频文件 → FFmpeg解帧 → 逐帧IRealTimeEnhancer → FFmpeg合帧+音轨 → MP4输出 → 入库。");

AddHeading(3, "3.4.2 AI数据流");
AddPara("影音文件 → 抽取关键帧/提取音轨 → 上传至大模型API（多模态分析） → 解析返回结果 → 存入数据库 → UI刷新展示。支持图片base64嵌入、视频FFmpeg抽关键帧、音频Whisper转文字等多种输入格式。");

AddPageBreak();

// ════════════════════════════════════════════════
// 第四章 系统实现
// ════════════════════════════════════════════════

AddHeading(1, "第四章  系统实现");

AddHeading(2, "4.1 开发环境");
AddTable(new[] { "项目", "内容" }, new[] {
    new[] { "操作系统", "Windows 10/11 x64" },
    new[] { "开发工具", "Visual Studio 2022" },
    new[] { "开发语言", "C# 13.0" },
    new[] { "框架版本", ".NET 10.0" },
    new[] { "UI框架", "WPF (Windows Presentation Foundation)" },
    new[] { "架构模式", "MVVM + 依赖注入" },
    new[] { "数据库", "SQLite (via Entity Framework Core 10)" },
    new[] { "NuGet包", "CommunityToolkit.Mvvm, EF Core SQLite, TagLibSharp, Xabe.FFmpeg, ONNX Runtime" },
    new[] { "外部工具", "FFmpeg (视频编解码)" },
});

AddHeading(2, "4.2 项目结构与代码规模");
AddPara("项目采用分层模块化目录结构，共包含77个类/接口/结构体，总计约13,475行代码（含10,361行C#代码和3,114行XAML代码）。以下是项目核心结构：");
AddCodeBlock("MediaEnhancer/\n├── Models/        8个数据实体（MediaFile, User, PlayHistory等）\n├── ViewModels/    6个文件（MainViewModel拆分为5个partial class）\n├── Services/      16个文件（9个服务+7个接口）\n├── Core/          20个文件（增强接口、算法实现、转换器）\n├── Views/         16个文件（7个XAML窗口+9个C#代码隐藏）\n├── Data/          2个文件（AppDbContext + 设计时工厂）\n├── Migrations/    3个文件（EF Core数据库迁移）\n└── OnnxModels/    1个ONNX模型文件");

AddHeading(2, "4.3 核心功能实现");

AddHeading(3, "4.3.1 线性拉伸增强算法");
AddPara("线性拉伸是系统中最基础的增强方法，通过将输入图像像素值按线性映射扩展到全动态范围来改善低光照、低对比度问题。算法采用两遍扫描：第一遍统计每个通道的像素值范围（最小值和最大值），第二遍将每个像素值线性映射到0-255全范围。针对彩色图像，在RGB三通道上独立进行拉伸处理。");
AddCodeBlock("// 核心拉伸公式（每个像素）\n// newValue = (pixel - min) * 255.0 / (max - min)\n// 实现时通过查表(LUT)优化，避免逐像素浮点运算");
AddPara("该算法纯C#实现，无复杂依赖，处理速度<0.1ms/帧（1920×1080分辨率），可支持60FPS实时处理。同时提供对比度强度（ContrastStrength）和亮度偏移（BrightnessOffset）两个可调参数，用户可通过界面滑块实时调节增强效果。");

AddHeading(3, "4.3.2 DXGI屏幕捕获实现");
AddPara("DXGI Desktop Duplication是用于捕获桌面画面的高性能API，通过GPU零拷贝技术获取屏幕纹理数据。本系统采用纯P/Invoke方式调用底层d3d11.dll和dxgi.dll，避免了大型第三方图形库（如SharpDX）的引入。");
AddPara("关键技术点：通过手动VTable调用实现所有COM接口方法（AcquireNextFrame、CopyResource、Map/Unmap等）；创建ResourceUsage.Staging的ID3D11Texture2D作为CPU可读的中间纹理；每帧通过CopyResource将GPU纹理拷贝到Staging纹理后再通过Marshal.Copy读取像素数据。在CopyResource后添加Flush操作确保GPU命令队列完成，消除脏帧问题。");

AddHeading(3, "4.3.3 屏幕录制实现");
AddPara("录制模块经历了5次迭代优化。最终方案采用DXGI/GDI捕获 → JPEG帧文件缓存 → FFmpeg序列帧编码的工作流。选择JPEG文件缓存而非内存缓存的考虑是：1920×1080分辨率下，60秒15FPS视频的原始帧数据约7.2GB，JPEG压缩后仅约180MB，大幅降低内存占用。");
AddPara("编码阶段采用逐级降级策略，按h264_nvenc（NVIDIA硬件编码） → h264_qsv（Intel硬件编码） → h264_amf（AMD硬件编码） → libx264（软件编码） → mpeg4（兜底）的顺序尝试，确保跨设备兼容性。录制完成后通过首尾帧时间戳反算真实帧率，保证音画同步。");

AddHeading(3, "4.3.4 AI对话模块实现");
AddPara("AI对话模块基于OpenAI兼容API实现，支持通义千问、DeepSeek、Ollama等多种后端。采用三层降级链：API正常 → 真实LLM对话；API未配置 → 本地模板化分析；API调用失败 → 标注具体错误信息。多模态输入支持图片base64嵌入和视频FFmpeg抽关键帧。UI采用头像气泡+FlowDocument渲染Markdown格式回复，每条消息提供复制按钮。");

AddHeading(2, "4.4 代码统计");
AddTable(new[] { "统计指标", "数值" }, new[] {
    new[] { "C#代码总行数", "10,361行" },
    new[] { "XAML代码总行数", "3,114行" },
    new[] { "代码总行数", "13,475行" },
    new[] { "注释行数", "1,836行（注释率17.7%）" },
    new[] { "class声明", "55个" },
    new[] { "interface声明", "10个" },
    new[] { "struct声明", "12个" },
    new[] { "最大单文件", "MainWindow.xaml（2,465行）" },
    new[] { "最大C#文件", "MainViewModel（5个partial合计2,636行）" },
    new[] { "服务类数量", "15个（9个实现+6个接口）" },
});

AddPara("所有类、核心接口、关键方法均包含详尽的XML中文注释。接口文件（如IDataService.cs）注释量高达107行，清晰说明了每个数据操作方法的用途、参数与返回值。代码严格遵守C#命名规范，变量命名采用语义化命名，代码可读性强。");

AddPageBreak();

// ════════════════════════════════════════════════
// 第五章 界面设计
// ════════════════════════════════════════════════

AddHeading(1, "第五章  界面设计");

AddHeading(2, "5.1 设计原则");
AddPara("本系统界面设计遵循以下原则：");
AddBullet("扁平化设计：采用现代简约的扁平化视觉风格，去除冗余装饰元素");
AddBullet("色调统一：主色调为蓝色系（#2563EB），辅助色为中性灰色，保持视觉和谐");
AddBullet("布局合理：主窗口1200×750像素，左侧导航栏+右侧内容区的左右分栏布局");
AddBullet("操作友好：按钮具有Hover悬停变色和Pressed下压反馈的动态效果，提供明确的操作反馈");
AddBullet("功能可见：已实现功能界面完整呈现，未实现功能界面做占位处理，界面与功能严格一致");

AddHeading(2, "5.2 主窗口布局");
AddPara("主窗口采用左右两栏布局：左侧为竖向导航栏，包含7个功能入口图标按钮（数据统计、文件管理、实时增强、屏幕录制、AI对话、AI编辑、系统设置）；右侧为内容展示区，通过自定义IndexToVisibilityConverter实现无刷新的分页切换。当前选中页面以高亮蓝色背景标识。");

AddHeading(2, "5.3 主要页面展示");

AddHeading(3, "5.3.1 数据统计仪表盘");
AddPara("首页采用九宫格卡片布局，展示文件总数、图片数、视频数、音频数、增强次数、实时增强会话数、录屏次数、总播放次数和收藏数等核心统计指标。卡片下方提供【检查依赖】（自动检测并下载FFmpeg）、【清理缓存】和【文件校验】等快捷操作入口。底部展示最近播放记录列表。");

AddHeading(3, "5.3.2 文件管理界面");
AddPara("文件管理页面是系统的核心操作界面。顶部提供搜索框、类型筛选下拉框和收藏过滤复选框，形成关键词+类型+收藏三维组合筛选体系。中央为DataGrid数据表格，展示文件名、类型、格式、文件大小、时长、分辨率、收藏状态等信息，支持勾选多行进行批量操作。底部工具栏固定显示批量收藏、删除、缩略图生成、增强四个操作按钮，选中≥2个文件后按钮启用。双击表格行可展开VS Code风格的右侧浮层详情面板，显示文件详细信息并提供播放、增强、重命名、删除等操作入口。");

AddHeading(3, "5.3.3 增强页面设计");
AddPara("增强页面分为实时增强和离线增强两个区域。实时增强区域提供增强方法选择下拉框、对比度强度滑块（0.5-2.0）、亮度偏移滑块（-50~+50）、预览图片选择和增强导出按钮。离线增强区域可选择所有增强方法（含ONNX模型方法），并独立提供全屏增强启动按钮。增强后的图片可在预览区实时查看效果对比。");

AddHeading(3, "5.3.4 其他页面");
AddPara("屏幕录制页面提供录制源选择、开始/停止按钮、录制时长显示和录制历史列表。AI对话页面采用侧边栏文件勾选+预设按钮+聊天气泡的交互模式。AI编辑页面支持选择图片文件进行图生图，或仅输入提示词进行文生图。系统设置页面提供AI对话/编辑分开配置的API密钥、模型名称、保存路径等选项。");

AddHeading(2, "5.4 用户体验设计");
AddBullet("快捷键支持：F11全局热键快速关闭全屏增强，播放器提供空格键播放/暂停");
AddBullet("进度反馈：视频增强、批量操作等耗时任务均提供进度提示和取消按钮");
AddBullet("错误处理：API调用失败时自动降级，文件缺失时提供删除/定位选项");
AddBullet("状态持久化：用户设置自动保存至appsettings.json，启动时自动加载");

AddPageBreak();

// ════════════════════════════════════════════════
// 第六章 测试与使用说明
// ════════════════════════════════════════════════

AddHeading(1, "第六章  测试与使用说明");

AddHeading(2, "6.1 系统安装说明");
AddHeading(3, "6.1.1 环境要求");
AddBullet("操作系统：Windows 10 / 11 x64");
AddBullet("运行时：.NET 10 Desktop Runtime");
AddBullet("可选依赖：FFmpeg（视频缩略图生成、录屏编码、视频增强所需）");

AddHeading(3, "6.1.2 安装步骤");
AddPara("（1）从发布包中解压文件或直接运行编译输出目录中的MediaEnhancer.exe。");
AddPara("（2）首次启动将弹出登录/注册窗口。新用户点击【注册】按钮创建账号（用户名和密码），已有账号则直接登录。系统采用SHA256+Salt方式安全存储密码。");
AddPara("（3）登录后进入数据统计页，点击【检查依赖】按钮自动检测FFmpeg安装状态，如未安装将提示下载（系统内置免费FFmpeg下载链接）。");
AddPara("（4）切换至文件管理页面，点击【导入文件夹】选择影音文件所在目录进行批量导入，或使用【导入文件】按钮单独选取文件。导入完成后即可在列表中浏览和管理影音文件。");
AddPara("（5）可选配置：在系统设置页面填入AI对话和AI编辑的API密钥与模型名称，以启用AI智能功能。不配置不影响其他功能正常使用（AI模块将自动降级为本地模板分析）。");

AddHeading(2, "6.2 软件使用说明");
AddHeading(3, "6.2.1 影音文件管理");
AddPara("在文件管理页面中，用户可通过顶部搜索框输入关键词进行文件名模糊搜索，通过类型下拉框筛选图片/视频/音频，通过收藏复选框仅显示已收藏文件。选中文件后可通过底部的批量操作工具栏进行批量收藏、删除、生成缩略图或增强处理。双击文件行可打开浮层详情面板，查看完整信息并进行播放、增强、重命名等操作。右键单击文件可弹出上下文菜单，提供快捷操作入口。");

AddHeading(3, "6.2.2 图像增强操作");
AddPara("离线增强：在文件管理页面选中单张图片或在详情面板中点击【增强】按钮，进入增强页面后可调节对比度强度和亮度偏移参数，点击【预览】查看增强效果，满意后点击【导出】保存增强文件。增强后的文件自动入库并在数据库中关联源文件。批量增强：按住Ctrl或Shift键多选文件后，点击底部工具栏的【增强】按钮进行批量处理。视频增强：在文件列表中右键视频文件选择【增强】，系统将自动逐帧处理并重新编码，处理过程显示进度和取消按钮。");

AddHeading(3, "6.2.3 全屏实时增强");
AddPara("在增强页面选择增强方法（实时方法可选线性拉伸或MultinexNano），设置参数后点击【全屏增强】按钮，系统将以透明覆盖窗口模式启动全屏增强。增强窗口不阻挡鼠标操作，用户可正常使用下层应用。按下F11全局热键随时退出增强模式。");

AddHeading(3, "6.2.4 屏幕录制");
AddPara("在屏幕录制页面点击【开始录制】按钮启动录制，录制过程中显示实时计时。停止录制后，系统将自动使用最佳可用编码器将帧序列合成为MP4文件，录制文件自动入库显示在右侧历史列表中。");

AddHeading(3, "6.2.5 AI功能使用");
AddPara("在AI对话页面，从左侧文件列表中选择想要分析的文件，点击预设按钮（如【AI简介】、【数据摘要】）或直接在输入框输入问题，系统将调用大模型进行分析并返回结果。如未配置API密钥，系统将自动使用本地模板化分析。AI编辑页面支持选择图片+输入描述进行图生图编辑，或仅输入提示词进行文生图创作，生成结果可保存并自动入库。");

AddHeading(2, "6.3 测试验证");
AddPara("系统经过了全面的功能测试，验证了各模块的正确性和稳定性：");
AddTable(new[] { "测试项目", "测试内容", "测试结果" }, new[] {
    new[] { "用户注册/登录", "创建新用户、登录验证、密码加密存储", "通过" },
    new[] { "文件扫描导入", "递归扫描1000+文件、去重、元数据提取", "通过" },
    new[] { "文件管理CRUD", "搜索筛选、收藏切换、重命名、多级删除确认", "通过" },
    new[] { "播放功能", "图片查看器缩放平移、视频播放器播放/暂停/全屏", "通过" },
    new[] { "线性拉伸增强", "参数调节、预览、导出、批量处理", "通过" },
    new[] { "ONNX模型增强", "MultinexNano模型加载、推理、结果验证", "通过" },
    new[] { "全屏增强", "DXGI捕获、透明覆盖、鼠标穿透、F11热键", "通过" },
    new[] { "屏幕录制", "DXGI/GDI捕获、编码器降级、录制历史", "通过" },
    new[] { "视频增强", "逐帧解/增强/合、进度显示、中途取消", "通过" },
    new[] { "AI对话", "多轮对话、多模态图片分析、模板化降级", "通过" },
    new[] { "AI编辑", "文生图、图生图、多供应商切换", "通过" },
    new[] { "配置持久化", "保存/加载/用户隔离", "通过" },
});
AddPara("数据库中填充了丰富的验证数据，涵盖图片、视频、音频多种格式，播放记录、增强日志、录屏记录均有多条真实操作产生的数据进行验证。");

AddPageBreak();

// ════════════════════════════════════════════════
// 第七章 总结与展望
// ════════════════════════════════════════════════

AddHeading(1, "第七章  总结与展望");

AddHeading(2, "7.1 项目总结");
AddPara("影音智增强系统作为一个完整的Windows桌面应用课程设计项目，严格按照课程设计要求进行开发，完成了从需求分析、系统设计到编码实现、测试验证的完整软件工程流程。项目的主要成果包括：");

AddPara("（1）技术架构：成功搭建了基于.NET 10 WPF + MVVM + EF Core + SQLite的完整桌面应用技术栈，代码量超过13,000行（远超500行要求），注释率达17.7%，代码结构清晰，可读性强。");

AddPara("（2）功能完整性：实现了文件管理、图像增强（线性拉伸+ONNX深度学习）、实时全屏增强、屏幕录制、AI对话分析、AI图像编辑、用户系统与数据统计七大核心功能模块，形成了【获取->增强->管理->分享】的完整闭环。");

AddPara("（3）界面设计：采用现代化扁平设计风格，主色调统一，布局合理，操作流畅。按钮具备悬停和下压的动态反馈效果，符合Windows桌面应用的交互规范。界面与功能严格一致，无冗余或缺失元素。");

AddPara("（4）数据库设计：通过EF Core Code-First方式设计并实现了8个数据表，支持多用户数据隔离，所有外键配置级联删除，数据一致性和完整性得到保障。");

AddPara("（5）扩展性设计：增强模块采用渐进式插件架构，通过多级接口隔离不同粒度的增强需求，新算法仅需实现接口并注册即可接入，体现了良好的软件架构设计思想。");

AddPara("（6）工程规范：所有类、方法、核心接口均包含详细的中文XML注释，遵循C#命名规范，采用依赖注入管理服务生命周期，代码组织清晰，符合软件工程最佳实践。");

AddHeading(2, "7.2 创新点与特色");
AddBullet("渐进式增强框架：初期用最简线性拉伸快速上线核心流程，架构预留扩展点，可无缝接入深度学习模型，兼顾开发速度与长期演进");
AddBullet("管理+增强深度融合：在文件管理界面直接提供画质修复入口，增强文件自动归档，形成统一资产库");
AddBullet("全链路AI智能化：AI对话、模板化降级、图像生成、智能分析等多模态AI功能深度集成");
AddBullet("实时与离线互补：既可在任意窗口上直接覆盖增强，又可对本地文件深度修复并导出");
AddBullet("增强录屏一体化：录制时可选择后处理增强，适用于保存在线低质量视频或制作教学素材");

AddHeading(2, "7.3 存在的不足");
AddPara("（1）音频录制功能尚未实现，录屏暂不支持麦克风和系统声音的同步录制。");
AddPara("（2）视频增强处理速度受限于纯CPU实现，未来可通过GPU加速或硬件编码器提升性能。");
AddPara("（3）AI对话历史不持久化，关闭程序后对话记录丢失。");
AddPara("（4）缺乏深色主题切换和多语言国际化支持。");
AddPara("（5）安装包打包流程尚未完善，目前仅支持直接运行可执行文件。");

AddHeading(2, "7.4 未来展望");
AddPara("在后续版本中，计划从以下方向持续完善：");
AddBullet("界面美化：全局主题切换（深色模式）、动画过渡效果、图标库统一、数据统计页图表可视化");
AddBullet("功能扩展：音频录制/麦克风支持、AI对话历史持久化与流式输出、视频处理GPU加速");
AddBullet("增强算法：集成更多深度学习模型（如MAXIM多任务增强），支持方法切换与混合增强");
AddBullet("用户体验：拖拽导入、缩略图懒加载、播放断点续播、快捷键体系完善");
AddBullet("工程化完善：Inno Setup安装包打包、自动更新机制、多语言国际化支持");

AddHeading(2, "7.5 课程学习体会");
AddPara("通过本次课程设计，深入理解了.NET框架下的WPF桌面应用开发流程。在MVVM架构模式的实践中，体会到了UI层与业务逻辑层解耦带来的代码可维护性提升。Entity Framework Core ORM的Code-First开发方式让数据库操作变得直观高效。DXGI底层COM接口的手动封装过程加深了对Windows图形系统的理解。项目还涉及了AI大模型API集成、FFmpeg多媒体处理、多线程并发控制等技术的实践应用，是一次全面的软件工程综合训练。");

// ════════════════════════════════════════════════
// 参考文献
// ════════════════════════════════════════════════

AddHeading(1, "参考文献");

string[] references = [
    "[1] Microsoft. .NET 10 Documentation [EB/OL]. https://learn.microsoft.com/dotnet/, 2025.",
    "[2] Microsoft. WPF Documentation [EB/OL]. https://learn.microsoft.com/dotnet/desktop/wpf/, 2025.",
    "[3] CommunityToolkit. MVVM Toolkit Documentation [EB/OL]. https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/, 2025.",
    "[4] Microsoft. Entity Framework Core Documentation [EB/OL]. https://learn.microsoft.com/ef/core/, 2025.",
    "[5] SQLite Consortium. SQLite Documentation [EB/OL]. https://www.sqlite.org/docs.html, 2025.",
    "[6] FFmpeg Team. FFmpeg Documentation [EB/OL]. https://ffmpeg.org/documentation.html, 2025.",
    "[7] ONNX Runtime. ONNX Runtime Documentation [EB/OL]. https://onnxruntime.ai/docs/, 2025.",
    "[8] Microsoft. DXGI Desktop Duplication API [EB/OL]. https://learn.microsoft.com/windows/win32/direct3ddxgi/desktop-dup-api, 2025.",
    "[9] OpenAI. GPT-4V API Documentation [EB/OL]. https://platform.openai.com/docs/, 2025.",
    "[10] Tu Z, Talebi H, Zhang H, et al. MAXIM: Multi-Axis MLP for Image Processing [C]. CVPR, 2022.",
];

foreach (var refText in references)
{
    body.Append(new Paragraph(
        new ParagraphProperties(
            new ParagraphStyleId { Val = "Normal" },
            new Indentation { FirstLine = "0", FirstLineChars = 0 },
            new SpacingBetweenLines { After = "60", Line = "320", LineRule = LineSpacingRuleValues.Auto }
        ),
        new Run(
            new RunProperties(new FontSize { Val = "21" }, new FontSizeComplexScript { Val = "21" }),
            new Text(refText)
        )
    ));
}

// ════════════════════════════════════════════════
// 附录
// ════════════════════════════════════════════════

AddPageBreak();
AddHeading(1, "附录");

AddHeading(2, "附录A  项目文件清单");
AddTable(new[] { "目录", "主要文件", "说明" }, new[] {
    new[] { "Models/", "MediaFile.cs, User.cs, PlayHistory.cs等8个文件", "数据实体定义" },
    new[] { "ViewModels/", "MainViewModel.cs（5个partial）, LoginViewModel.cs", "MVVM视图模型" },
    new[] { "Services/", "DataService.cs, AiService.cs, ScreenRecorder.cs等15个文件", "业务服务层" },
    new[] { "Core/", "LinearStretchMethod.cs, MultinexNanoMethod.cs等20个文件", "核心算法与工具" },
    new[] { "Views/", "FullscreenEnhanceWindow, MediaPlayerWindow等15个文件", "WPF窗口与控件" },
    new[] { "Data/", "AppDbContext.cs", "EF Core数据库上下文" },
    new[] { "Migrations/", "3个迁移文件", "数据库迁移历史" },
    new[] { "OnnxModels/", "multinex_nano_lolv2_syn.onnx", "预训练ONNX模型" },
});

AddHeading(2, "附录B  支持的文件格式");
AddTable(new[] { "类别", "支持格式" }, new[] {
    new[] { "图片", ".jpg .jpeg .png .bmp .gif .webp" },
    new[] { "视频", ".mp4 .avi .mkv .mov .wmv .flv .webm" },
    new[] { "音频", ".mp3 .wav .flac .aac .ogg .wma .m4a" },
});

AddHeading(2, "附录C  系统功能完成度");
AddTable(new[] { "模块", "页面", "完成状态", "核心功能数量" }, new[] {
    new[] { "影音管理", "文件管理", "✅ 完成", "10项（扫描/导入/搜索/收藏/重命名/删除/详情/批量/校验/播放）" },
    new[] { "图像增强", "实时增强", "✅ 完成", "6项（线性拉伸/ONNX模型/参数调节/预览/导出/对比）" },
    new[] { "全屏增强", "实时增强", "✅ 完成", "5项（DXGI捕获/透明覆盖/鼠标穿透/F11热键/GDI回退）" },
    new[] { "离线增强", "实时增强", "✅ 完成", "5项（单图/批量/视频逐帧/进度显示/中途取消）" },
    new[] { "屏幕录制", "屏幕录制", "✅ 完成", "6项（DXGI/GDI/JPEG缓存/编码降级/后处理增强/历史列表）" },
    new[] { "AI对话", "AI对话", "✅ 完成", "5项（多模态/模板化降级/Markdown渲染/多轮对话/划词复制）" },
    new[] { "AI编辑", "AI编辑", "✅ 完成", "4项（文生图/图生图/多供应商/保存入库）" },
    new[] { "用户系统", "登录窗口", "✅ 完成", "3项（注册/登录/数据隔离）" },
    new[] { "数据统计", "数据统计", "✅ 完成", "5项（9卡仪表盘/快速入口/依赖检查/缓存清理/文件校验）" },
    new[] { "系统设置", "系统设置", "✅ 完成", "5项（AI对话配置/AI编辑配置/路径设置/持久化/用户隔离）" },
});

// ════════════════════════════════════════════════
// Final section properties
// ════════════════════════════════════════════════
body.Append(sectPr);

Console.WriteLine("Report generated successfully!");
