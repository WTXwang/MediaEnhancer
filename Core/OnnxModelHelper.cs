using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MediaEnhancer.Core;

/// <summary>
/// ONNX 模型推理的通用预处理/后处理工具。
/// 负责 BitmapSource ↔ NCHW float32 的格式转换。
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
    /// 双线性缩放 BGRA32 像素数组。
    /// </summary>
    public static byte[] ResizeBilinear(byte[] src, int srcW, int srcH, int srcStride,
                                        int dstW, int dstH)
    {
        int dstStride = dstW * 4;
        byte[] dst = new byte[dstStride * dstH];
        double scaleX = (double)srcW / dstW;
        double scaleY = (double)srcH / dstH;

        for (int dy = 0; dy < dstH; dy++)
        {
            double sy = dy * scaleY;
            int y0 = (int)sy;
            int y1 = Math.Min(y0 + 1, srcH - 1);
            double fy = sy - y0;

            for (int dx = 0; dx < dstW; dx++)
            {
                double sx = dx * scaleX;
                int x0 = (int)sx;
                int x1 = Math.Min(x0 + 1, srcW - 1);
                double fx = sx - x0;

                int dIdx = dy * dstStride + dx * 4;
                int s00 = y0 * srcStride + x0 * 4;
                int s01 = y0 * srcStride + x1 * 4;
                int s10 = y1 * srcStride + x0 * 4;
                int s11 = y1 * srcStride + x1 * 4;

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
    /// BGRA32 byte[] → NCHW float32 [0,1] RGB（跳过 BitmapSource，零分配转换）。
    /// 若 maxSize > 0 且输入超过此值，先缩放到目标尺寸再转换。
    /// 返回 (nchw, inferenceW, inferenceH, originalW, originalH, downscaled)。
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
            work = ResizeBilinear(pixels, width, height, stride, iw, ih);
            workStride = iw * 4;
        }

        float[] result = new float[3 * ih * iw];
        for (int y = 0; y < ih; y++)
        {
            for (int x = 0; x < iw; x++)
            {
                int srcIdx = y * workStride + x * 4;
                int dstIdx = y * iw + x;
                result[0 * ih * iw + dstIdx] = work[srcIdx + 2] / 255f;
                result[1 * ih * iw + dstIdx] = work[srcIdx + 1] / 255f;
                result[2 * ih * iw + dstIdx] = work[srcIdx + 0] / 255f;
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

    private static byte[] PostprocessDirect(float[] data, int height, int width, int stride)
    {
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcIdx = y * width + x;
                int dstIdx = y * stride + x * 4;
                pixels[dstIdx + 0] = ClampToByte(data[0 * height * width + srcIdx] * 255f);
                pixels[dstIdx + 1] = ClampToByte(data[1 * height * width + srcIdx] * 255f);
                pixels[dstIdx + 2] = ClampToByte(data[2 * height * width + srcIdx] * 255f);
                pixels[dstIdx + 3] = 255;
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
    /// 运行 ONNX 推理：NCHW input → NCHW output。
    /// </summary>
    public static float[] RunInference(InferenceSession session,
        float[] input, int height, int width)
    {
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

    private static byte ClampToByte(float v) =>
        (byte)Math.Clamp((int)(v + 0.5f), 0, 255);
}
