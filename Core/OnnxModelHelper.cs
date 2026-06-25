using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MediaEnhancer.Core;

/// <summary>
/// ONNX 模型推理的通用预处理/后处理工具（静态类）。
///
/// 核心职责：在 WPF 图像格式和 ONNX 模型格式之间做双向转换。
/// WPF 侧：BitmapSource / BGRA32 byte[]（[B, G, R, A] 每像素 4 字节）
/// ONNX 侧：NCHW float32 [0.0, 1.0]（[Channel, Height, Width]，RGB 通道序）
///
/// 两个入口路径：
///   - byte[] 路径：供 IRealTimeEnhancer.Enhance(byte[]) 调用（全屏增强/视频增强）
///   - BitmapSource 路径：供 IOnnxEnhancement.EnhanceAsync(BitmapSource) 调用（离线增强）
///
/// 降采样策略：当 maxSize > 0 且输入长边超过该值时，先 Bilinear 缩放到目标尺寸再推理，
/// 推理结果再放大回原始尺寸。适合实时场景以速度换质量。
/// </summary>
public static class OnnxModelHelper
{
    // ================================================================
    // 降采样 — 推理尺寸计算
    // ================================================================

    /// <summary>
    /// 根据原始尺寸和 maxSize 计算推理用的目标尺寸（保持宽高比）。
    /// maxSize 为 0 表示不缩放，返回原始尺寸。
    /// </summary>
    public static (int w, int h) GetInferenceSize(int width, int height, int maxSize)
    {
        if (maxSize <= 0 || (width <= maxSize && height <= maxSize))
            return (width, height);

        double scale = (double)maxSize / Math.Max(width, height);
        return ((int)(width * scale), (int)(height * scale));
    }

    /// <summary>
    /// 双线性插值缩放 BGRA32 像素数组。
    /// 对每个目标像素，找到它在源图中的 4 个邻居像素，按水平+垂直权重混合。
    /// 同时对 BGRA 四个通道做插值，Alpha 通道也随之缩放（如 1920→480 边缘平滑）。
    /// </summary>
    public static byte[] ResizeBilinear(byte[] src, int srcW, int srcH, int srcStride,
                                        int dstW, int dstH)
    {
        int dstStride = dstW * 4;
        byte[] dst = new byte[dstStride * dstH];
        // 缩放因子：>1 表示放大，<1 表示缩小
        double scaleX = (double)srcW / dstW;
        double scaleY = (double)srcH / dstH;

        for (int dy = 0; dy < dstH; dy++)
        {
            // 目标像素对应的源图浮点坐标 (sx, sy)
            double sy = dy * scaleY;
            int y0 = (int)sy;                              // 上方邻居行
            int y1 = Math.Min(y0 + 1, srcH - 1);           // 下方邻居行（边界钳位）
            double fy = sy - y0;                            // 垂直小数部分（权重）

            for (int dx = 0; dx < dstW; dx++)
            {
                double sx = dx * scaleX;
                int x0 = (int)sx;                          // 左侧邻居列
                int x1 = Math.Min(x0 + 1, srcW - 1);       // 右侧邻居列（边界钳位）
                double fx = sx - x0;                        // 水平小数部分（权重）

                int dIdx = dy * dstStride + dx * 4;  // 目标像素 BGRA 起始偏移
                // 四角邻居像素的 BGRA 起始偏移
                int s00 = y0 * srcStride + x0 * 4;  // 左上
                int s01 = y0 * srcStride + x1 * 4;  // 右上
                int s10 = y1 * srcStride + x0 * 4;  // 左下
                int s11 = y1 * srcStride + x1 * 4;  // 右下

                // 对 B, G, R, A 四个通道分别双线性插值
                // 公式：先按 fx 混合水平邻居，再按 fy 混合上下结果
                for (int c = 0; c < 4; c++)
                {
                    double v = (1 - fy) * ((1 - fx) * src[s00 + c] + fx * src[s01 + c])
                             + fy * ((1 - fx) * src[s10 + c] + fx * src[s11 + c]);
                    dst[dIdx + c] = (byte)Math.Clamp((int)(v + 0.5), 0, 255);
                }
            }
        }
        return dst;
    }

    // ================================================================
    // byte[] 路径（供 IRealTimeEnhancer 逐帧调用：全屏增强 / 视频增强）
    // ================================================================

    /// <summary>
    /// 预处理（byte[] 路径）：BGRA32 → NCHW float32 [0,1]。
    ///
    /// 转换细节：
    ///   WPF/Bitmap 使用 BGRA32 像素格式，内存布局为 [B, G, R, A]。
    ///   ONNX 模型要求 NCHW 格式，即：
    ///     N = batch=1（单张图片）
    ///     C = 3（R, G, B 三通道）
    ///     H = height, W = width
    ///   且值域为 [0.0, 1.0]（原始 uint8 除以 255）。
    ///
    /// 若 maxSize > 0 且输入超过此值，先 Bilinear 缩放到目标尺寸再转换。
    /// 返回元组包含 NCHW 数据、推理尺寸、原始尺寸及是否降采样标志。
    /// </summary>
    public static (float[] data, int infW, int infH, int origW, int origH, bool downscaled)
        Preprocess(byte[] pixels, int width, int height, int stride, int maxSize)
    {
        var (iw, ih) = GetInferenceSize(width, height, maxSize);
        bool downscaled = iw != width || ih != height;

        byte[] work = pixels;
        int workStride = stride;
        if (downscaled)
        {
            // 先缩放到推理尺寸，减少后续 ONNX 计算量
            work = ResizeBilinear(pixels, width, height, stride, iw, ih);
            workStride = iw * 4;
        }

        // BGRA32 byte[] → NCHW float32
        // 布局：result[C][Y][X] = result[C * H * W + Y * W + X]
        // C=0:R, C=1:G, C=2:B
        float[] result = new float[3 * ih * iw];
        for (int y = 0; y < ih; y++)
        {
            for (int x = 0; x < iw; x++)
            {
                int srcIdx = y * workStride + x * 4;   // BGRA 像素位置
                int dstIdx = y * iw + x;                // NCHW 平面内偏移
                // BGRA[+2]=R → NCHW[0], BGRA[+1]=G → NCHW[1], BGRA[+0]=B → NCHW[2]
                result[0 * ih * iw + dstIdx] = work[srcIdx + 2] / 255f;  // R 通道
                result[1 * ih * iw + dstIdx] = work[srcIdx + 1] / 255f;  // G 通道
                result[2 * ih * iw + dstIdx] = work[srcIdx + 0] / 255f;  // B 通道
            }
        }
        return (result, iw, ih, width, height, downscaled);
    }

    /// <summary>
    /// NCHW float32 [0,1] RGB → BGRA32 byte[]。
    /// 若推理时做过降采样，自动放大回原始分辨率。
    /// </summary>
    public static byte[] Postprocess(float[] data, int infH, int infW,
        int origW, int origH, int origStride, bool downscaled)
    {
        if (!downscaled)
            return PostprocessDirect(data, infH, infW, origStride);

        // 先在推理尺寸下生成 BGRA，再放大到原始尺寸
        byte[] small = PostprocessDirect(data, infH, infW, infW * 4);
        return ResizeBilinear(small, infW, infH, infW * 4, origW, origH);
    }

    /// <summary>
    /// 后处理（直出版本，无缩放）：NCHW float32 → BGRA32 byte[]。
    /// 逆操作：C=0(R)→BGRA[+2], C=1(G)→BGRA[+1], C=2(B)→BGRA[+0]。
    /// ONNX 输出未必在 [0,1] 区间内，通过 ClampToByte 钳位。
    /// </summary>
    private static byte[] PostprocessDirect(float[] data, int height, int width, int stride)
    {
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = y * width + x;                       // NCHW 平面内偏移
                int dstIdx = y * stride + x * 4;                  // BGRA 像素位置
                // NCHW: C=0→R, C=1→G, C=2→B 逆映射到 BGRA 字节序
                pixels[dstIdx + 0] = ClampToByte(data[2 * height * width + srcIdx] * 255f); // B
                pixels[dstIdx + 1] = ClampToByte(data[1 * height * width + srcIdx] * 255f); // G
                pixels[dstIdx + 2] = ClampToByte(data[0 * height * width + srcIdx] * 255f); // R
                pixels[dstIdx + 3] = 255;                                                    // A（不透明白色）
            }
        }
        return pixels;
    }

    // ================================================================
    // BitmapSource 路径（供 IOnnxEnhancement.EnhanceAsync 离线增强）
    // ================================================================

    /// <summary>
    /// BitmapSource → BGRA byte[] → NCHW float32 [0,1] RGB。
    /// </summary>
    public static float[] Preprocess(BitmapSource source)
    {
        // 统一转为 BGRA32 格式
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        byte[] pixels = new byte[stride * h];
        bgra.CopyPixels(pixels, stride, 0);

        // BGRA → NCHW float32, [0,1]
        float[] result = new float[3 * h * w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int srcIdx = y * stride + x * 4;
                int dstIdx = y * w + x;
                // BGRA: [B, G, R, A] → NCHW-RGB
                result[0 * h * w + dstIdx] = pixels[srcIdx + 2] / 255f; // R
                result[1 * h * w + dstIdx] = pixels[srcIdx + 1] / 255f; // G
                result[2 * h * w + dstIdx] = pixels[srcIdx + 0] / 255f; // B
            }
        }
        return result;
    }

    /// <summary>
    /// NCHW float32 [0,1] RGB → BGRA32 BitmapSource。
    /// </summary>
    public static BitmapSource Postprocess(float[] data, int height, int width)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = y * width + x;
                int dstIdx = y * stride + x * 4;

                byte r = ClampToByte(data[0 * height * width + srcIdx] * 255f);
                byte g = ClampToByte(data[1 * height * width + srcIdx] * 255f);
                byte b = ClampToByte(data[2 * height * width + srcIdx] * 255f);

                pixels[dstIdx + 0] = b; // B
                pixels[dstIdx + 1] = g; // G
                pixels[dstIdx + 2] = r; // R
                pixels[dstIdx + 3] = 255; // A
            }
        }

        var bitmap = BitmapSource.Create(width, height, 96, 96,
            PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 运行 ONNX 推理：将 NCHW float32 数据打包为 4D Tensor，送入模型，取第一个输出。
    ///
    /// 输入 tensor 形状：[1, 3, height, width]（N=1 batch, C=3 RGB, H×W 空间）
    /// 输入名固定为 "input"，输出取 session.Run() 返回的第一个 tensor。
    /// 模型输出形状同样为 [1, 3, H, W]，值域通常 [0,1] 附近（建议 clamp）。
    /// </summary>
    public static float[] RunInference(InferenceSession session,
        float[] input, int height, int width)
    {
        // NCHW 4D tensor: batch=1, channels=3, spatial H×W
        var inputShape = new[] { 1, 3, height, width };
        var inputTensor = new DenseTensor<float>(input, inputShape);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor)
        };

        using var results = session.Run(inputs);
        var output = results.First().AsTensor<float>();
        return output.ToArray();
    }
    /// <summary>
    /// 将像素值约束在 0-255 之间
    /// </summary>
    private static byte ClampToByte(float v) =>
        (byte)Math.Clamp((int)(v + 0.5f), 0, 255);
}
