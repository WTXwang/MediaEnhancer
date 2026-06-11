using System.Collections.Generic;
using System.Linq;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 增强方法注册中心——管理所有 IRealTimeEnhancer 实例的注册、切换与枚举。
    ///
    /// 使用方式：
    ///   1. 启动时注册：registry.Register(new LinearStretchMethod());
    ///   2. UI 枚举：registry.MethodNames → 下拉框
    ///   3. 用户切换：registry.SetCurrent("直方图均衡化") → 参数面板刷新
    ///   4. 启动增强：window.Start(registry.Current, params)
    ///
    /// 消费者（全屏窗口、录屏器）只通过此注册中心获取当前算法，
    /// 不直接依赖任何具体实现类。
    /// </summary>
    public class EnhancementRegistry
    {
        private readonly Dictionary<string, IRealTimeEnhancer> _methods = new();
    private readonly Dictionary<string, IOnnxEnhancement> _offlineMethods = new();
    private IRealTimeEnhancer? _current;

    /// <summary>
    /// 获取当前选中的实时增强方法。如果从未设置则返回第一个注册的方法。
    /// </summary>
    public IRealTimeEnhancer? Current => _current;

    /// <summary>
    /// 获取当前选中方法的名称，未注册时返回 "无"。
    /// </summary>
    public string CurrentMethodName => _current?.Name ?? "无";

    /// <summary>
    /// 获取所有已注册的增强方法名称列表（实时 + 离线，按注册顺序）。
    /// </summary>
    public IReadOnlyList<string> MethodNames =>
        _methods.Keys.Concat(_offlineMethods.Keys).ToList();

    /// <summary>
    /// 获取已注册方法总数。
    /// </summary>
    public int Count => _methods.Count + _offlineMethods.Count;

    /// <summary>
    /// 获取所有离线增强方法名称列表。
    /// </summary>
    public IReadOnlyList<string> OfflineMethodNames => _offlineMethods.Keys.ToList();

    /// <summary>
    /// 注册一个实时增强方法。如果同名方法已存在则覆盖。
    /// 第一个注册的方法自动成为当前方法。
    /// </summary>
    public void Register(IRealTimeEnhancer method)
    {
        _methods[method.Name] = method;
        _current ??= method;
    }

    /// <summary>
    /// 注册一个 ONNX 深度学习增强方法。
    /// 离线方法不出现在实时增强列表中，但可供文件增强使用。
    /// </summary>
    public void RegisterOffline(IOnnxEnhancement method)
    {
        _offlineMethods[method.Name] = method;
    }

    /// <summary>
    /// 切换到指定名称的增强方法（仅限实时方法）。
    /// </summary>
    public bool SetCurrent(string methodName)
    {
        if (!_methods.TryGetValue(methodName, out var method))
            return false;

        _current = method;
        return true;
    }

    /// <summary>
    /// 按名称获取增强方法（实时或离线），未找到返回 null。
    /// </summary>
    public IEnhancementMethod? GetMethod(string methodName)
    {
        if (_methods.TryGetValue(methodName, out var realTime))
            return realTime;
        if (_offlineMethods.TryGetValue(methodName, out var offline))
            return offline;
        return null;
    }

    /// <summary>
    /// 按名称获取 ONNX 离线增强方法，未找到返回 null。
    /// </summary>
    public IOnnxEnhancement? GetOfflineMethod(string methodName)
    {
        return _offlineMethods.TryGetValue(methodName, out var method) ? method : null;
    }

    /// <summary>
    /// 获取所有已注册的实时增强方法名称列表。
    /// </summary>
    public IReadOnlyList<string> RealTimeMethodNames => _methods.Keys.ToList();
    }
}
