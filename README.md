# ChainExecutor.NetCoreApp 企业级链式执行框架 - 标准使用示例

## 1. 业务模型（带 DataAnnotation 验证）

```csharp
using System.ComponentModel.DataAnnotations;

/// <summary>
/// 用户创建模型（带自动验证特性）
/// </summary>
public class UserCreateModel
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户名（必填）
    /// </summary>
    [Required(ErrorMessage = "用户名不能为空")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 年龄（18~120岁）
    /// </summary>
    [Range(18, 120, ErrorMessage = "年龄必须≥18且≤120")]
    public int Age { get; set; }
}

```

## 2. 链式调用代码（同步 / 异步双支持）

异步写法（推荐，生产标准）

```csharp
using ChainExecutor.NetCoreApp;

/// <summary>
/// 链式执行器 - 企业级标准用法
/// </summary>
public async Task<UserCreateModel> CreateUserAsync()
{
    // 1. 链式编排业务流程
    var result = await new ChainExecutor<UserCreateModel>()
        // 初始化模型数据
        .InitData(new UserCreateModel 
        { 
            Id = 1, 
            Username = "张三", 
            Age = 17  // 故意设为17，触发验证失败
        })
        
        // 自动验证 DataAnnotation 特性（[Required]/[Range]）
        .ValidateAnnotations()
        
        // 自定义业务规则验证
        .ValidateModel(m => !string.IsNullOrWhiteSpace(m.Username))
        
        // 业务逻辑处理
        .Process(ctx =>
        {
            // 处理业务数据
            ctx.Data!.Username = $"【正式用户】{ctx.Data.Username}";
            // 上下文临时存储（跨步骤共享数据）
            ctx.Items["BusinessStep"] = "用户信息格式化完成";
        })
        
        // 事务包裹（数据库操作统一提交/回滚）
        .UseTransaction(ctx =>
        {
            // 此处编写数据库写入逻辑
            // _dbContext.Add(ctx.Data);
            // _dbContext.SaveChanges();
        })
        
        // 最终执行
        .ExecuteAsync();

    // 2. 结果处理
    if (!result.IsSuccess)
    {
        Console.WriteLine($"执行失败：{result.ErrorMessage}");
        throw new CustomValidationException(result.ErrorMessage);
    }

    Console.WriteLine($"执行成功：{result.Data!.Username}");
    return result.Data;
}

```

同步写法

```csharp
public UserCreateModel CreateUserSync()
{
    var result = new ChainExecutor<UserCreateModel>()
        .InitData(new UserCreateModel { Id = 1, Username = "张三", Age = 20 })
        .ValidateAnnotations()
        .ValidateModel(m => m.Age >= 18)
        .Process(ctx => { })
        .UseTransaction(ctx => { })
        .Execute();

    return result.IsSuccess ? result.Data! : throw new Exception(result.ErrorMessage);
}
```

3. 执行结果与日志输出

```csharp
// 基础结果判断
if (result.IsSuccess)
{
    Console.WriteLine("✅ 业务执行成功");
    Console.WriteLine($"用户名称：{result.Data!.Username}");
}
else
{
    Console.WriteLine("❌ 业务执行失败");
    Console.WriteLine($"错误信息：{result.ErrorMessage}");
}

// 全流程日志输出（问题排查神器）
Console.WriteLine("\n===== 执行日志 =====");
foreach (var log in result.ExecuteLogs)
{
    Console.WriteLine(log);
}

// 性能耗时
Console.WriteLine($"\n总耗时：{result.ElapsedMilliseconds} ms");

```

## 4. 框架核心能力（企业级特性）

| 特性       | 说明                                                  |
|------------|-------------------------------------------------------|
| 🔗 链式编程 | 流程声明式编写，无嵌套、无冗余代码                    |
| 🛡️ 自动验证 | 支持 DataAnnotation 特性验证 + 自定义验证           |
| 📝 全链路日志 | 自动记录调用方、步骤、时间、状态                    |
| 🔒 异常隔离 | 自动捕获异常，中断后续流程，不污染业务                |
| 🧵 线程安全  | 内置线程安全参数字典，支持高并发                    |
| 🔄 同步 / 异步 | 完美支持同步 / 异步业务场景                         |
| 📊 性能监控 | 自动统计全流程执行耗时                                |
| 🎯 AOP 拦截 | 支持前置 / 后置 / 异常全局钩子                      |
| 💼 事务管理 | 内置事务包裹，统一提交 / 回滚                         |
| 🧩 依赖注入 | 支持 IServiceProvider 注入服务                      |



## 5. 典型应用场景
- 微服务业务流程编排
- 数据库事务统一管理
- API 接口统一逻辑处理
- 审批流 / 工作流节点执行
- 数据清洗与转换管道
- 后台定时任务编排
- 复杂业务规则校验


## 6. 框架优势
## 6. 框架优势
- ✅ 无侵入：不依赖任何第三方库，复制即用  
- ✅ 高兼容：完全向后兼容，原有代码无需修改  
- ✅ 易维护：流程清晰，日志完整，排查问题极快  
- ✅ 可扩展：支持 AOP、拦截、事务、缓存扩展  
- ✅ 生产级：企业标准架构，可直接上线使用  
