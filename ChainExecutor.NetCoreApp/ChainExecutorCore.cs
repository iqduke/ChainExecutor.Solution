using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ChainExecutor.NetCoreApp
{
	#region 自定义验证异常
	/// <summary>
	/// 自定义数据验证异常（专门用于数据验证场景的异常）
	/// </summary>
	public class CustomValidationException : Exception
	{
		/// <summary>
		/// 无参构造函数
		/// </summary>
		public CustomValidationException() : base() { }

		/// <summary>
		/// 带错误信息的构造函数
		/// </summary>
		/// <param name="message">验证错误信息</param>
		public CustomValidationException(string message) : base(message) { }

		/// <summary>
		/// 带错误信息和内部异常的构造函数
		/// </summary>
		/// <param name="message">验证错误信息</param>
		/// <param name="innerException">内部异常</param>
		public CustomValidationException(string message, Exception innerException)
			: base(message, innerException) { }
	}
	#endregion

	#region 泛型执行结果模型
	/// <summary>
	/// 泛型执行结果模型，封装错误信息、业务数据和执行日志
	/// </summary>
	/// <typeparam name="TData">业务数据的类型</typeparam>
	public class ChainExecuteResult<TData> where TData: class, new()
	{
		/// <summary>
		/// 执行是否成功
		/// </summary>
		public bool IsSuccess { get; set; }

		/// <summary>
		/// 错误信息（执行失败时赋值）
		/// </summary>
		public string ErrorMessage { get; set; } = string.Empty;

		/// <summary>
		/// 业务数据（执行成功时赋值）
		/// </summary>
		public TData Data { get; set; }

		/// <summary>
		/// 执行步骤日志（完整流程追踪）
		/// </summary>
		public List<string> ExecuteLogs { get; set; } = new List<string>();

		/// <summary>
		/// 快速创建成功结果实例
		/// </summary>
		/// <param name="data">业务数据</param>
		/// <param name="executeLogs">执行日志</param>
		/// <returns>成功结果实例</returns>
		public static ChainExecuteResult<TData> Success(TData data, List<string> executeLogs)
		{
			return new ChainExecuteResult<TData>
			{
				IsSuccess = true,
				Data = data,
				ExecuteLogs = executeLogs ?? new List<string>()
			};
		}

		/// <summary>
		/// 快速创建失败结果实例
		/// </summary>
		/// <param name="errorMessage">错误信息</param>
		/// <param name="executeLogs">执行日志</param>
		/// <returns>失败结果实例</returns>
		public static ChainExecuteResult<TData> Fail(string errorMessage, List<string> executeLogs)
		{
			return new ChainExecuteResult<TData>
			{
				IsSuccess = false,
				ErrorMessage = errorMessage,
				ExecuteLogs = executeLogs ?? new List<string>()
			};
		}
	}
	#endregion

	#region 泛型链式执行类
	/// <summary>
	/// 优化版泛型链式执行类（支持调用方类/方法追踪、日志、异步、缓存、自定义验证异常）
	/// </summary>
	/// <typeparam name="TData">最终返回数据的类型</typeparam>
	public class ChainExecutor<TData> where TData : class, new()
	{
		#region 私有字段
		// 业务数据
		private TData _innerData;
		// 错误信息
		private string _innerErrorMessage = string.Empty;
		// 错误标记
		private bool _hasError = false;
		// 步骤日志列表
		private List<string> _executeLogs = new List<string>();
		// 调用方信息（类名、方法名）
		private string _callerClassName = "未知类";
		private string _callerMethodName = "未知方法";
		#endregion

		#region 构造函数
		/// <summary>
		/// 构造函数：初始化并捕获调用方的类名和方法名
		/// </summary>
		public ChainExecutor()
		{
			CaptureCallerInfo();
			RecordLog(nameof(ChainExecutor), "链式执行类初始化完成（已捕获调用方信息）");
		}
		#endregion

		#region 私有辅助方法
		/// <summary>
		/// 捕获调用方的类名和方法名
		/// </summary>
		private void CaptureCallerInfo()
		{
			try
			{
				var stackTrace = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
				if (stackTrace.FrameCount > 0)
				{
					StackFrame callerFrame = stackTrace.GetFrame(0);
					if (callerFrame != null)
					{
						var callerMethod = callerFrame.GetMethod();
						if (callerMethod != null)
						{
							_callerClassName = callerMethod.DeclaringType != null ? callerMethod.DeclaringType.FullName : "未知类";
							_callerMethodName = callerMethod.Name ?? "未知方法";
						}
					}
				}
			}
			catch (Exception ex)
			{
				RecordLog(nameof(CaptureCallerInfo), $"调用方信息捕获失败：{ex.Message}", false);
			}
		}

		/// <summary>
		/// 记录步骤日志（融入调用方类/方法名）
		/// </summary>
		/// <param name="methodName">当前执行的内部方法名</param>
		/// <param name="description">描述信息</param>
		/// <param name="isSuccess">是否成功</param>
		private void RecordLog(string methodName, string description, bool isSuccess = true)
		{
			var log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] | 【{_callerClassName}.{_callerMethodName}】 | {methodName} | {(isSuccess ? "成功" : "失败")} | {description}";
			_executeLogs.Add(log);
		}
		#endregion

		#region 同步链式方法
		/// <summary>
		/// 初始化业务数据（同步）
		/// </summary>
		/// <param name="initData">初始化数据</param>
		/// <returns>当前实例</returns>
		public ChainExecutor<TData> InitData(TData initData)
		{
			if (_hasError) return this;

			try
			{
				_innerData = initData;
				RecordLog(nameof(InitData), "数据初始化完成");
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"数据初始化失败：{ex.Message}";
				RecordLog(nameof(InitData), _innerErrorMessage, false);
			}

			return this;
		}

		/// <summary>
		/// 处理业务逻辑（同步）
		/// </summary>
		/// <param name="processAction">业务处理委托</param>
		/// <returns>当前实例</returns>
		public ChainExecutor<TData> ProcessBusiness(Action<TData> processAction)
		{
			if (_hasError || processAction == null) return this;

			try
			{
				processAction.Invoke(_innerData);
				RecordLog(nameof(ProcessBusiness), "业务逻辑处理完成");
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"业务逻辑处理失败：{ex.Message}";
				RecordLog(nameof(ProcessBusiness), _innerErrorMessage, false);
			}

			return this;
		}

		/// <summary>
		/// 验证数据有效性（同步）
		/// </summary>
		/// <param name="validateFunc">数据验证委托</param>
		/// <returns>当前实例</returns>
		public ChainExecutor<TData> ValidateData(Func<TData, bool> validateFunc)
		{
			if (_hasError || validateFunc == null) return this;

			try
			{
				var isValid = validateFunc.Invoke(_innerData);
				if (!isValid)
				{
					throw new CustomValidationException("数据不符合业务规则（如：年龄必须≥18、字符串不能为空）");
				}
				RecordLog(nameof(ValidateData), "数据验证通过");
			}
			catch (CustomValidationException ex)
			{
				_hasError = true;
				_innerErrorMessage = $"数据验证失败：{ex.Message}";
				RecordLog(nameof(ValidateData), _innerErrorMessage, false);
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"数据验证异常（非验证类错误）：{ex.Message}";
				RecordLog(nameof(ValidateData), _innerErrorMessage, false);
			}

			return this;
		}
		#endregion

		#region 异步链式方法
		/// <summary>
		/// 初始化业务数据（异步）
		/// </summary>
		/// <param name="initDataFunc">异步初始化委托</param>
		/// <returns>当前实例</returns>
		public async Task<ChainExecutor<TData>> InitDataAsync(Func<Task<TData>> initDataFunc)
		{
			if (_hasError || initDataFunc == null) return this;

			try
			{
				_innerData = await initDataFunc.Invoke();
				RecordLog(nameof(InitDataAsync), "异步数据初始化完成");
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"异步数据初始化失败：{ex.Message}";
				RecordLog(nameof(InitDataAsync), _innerErrorMessage, false);
			}

			return this;
		}

		/// <summary>
		/// 处理业务逻辑（异步）
		/// </summary>
		/// <param name="processFunc">异步业务处理委托</param>
		/// <returns>当前实例</returns>
		public async Task<ChainExecutor<TData>> ProcessBusinessAsync(Func<TData, Task> processFunc)
		{
			if (_hasError || processFunc == null) return this;

			try
			{
				await processFunc.Invoke(_innerData);
				RecordLog(nameof(ProcessBusinessAsync), "异步业务逻辑处理完成");
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"异步业务逻辑处理失败：{ex.Message}";
				RecordLog(nameof(ProcessBusinessAsync), _innerErrorMessage, false);
			}

			return this;
		}

		/// <summary>
		/// 验证数据有效性（异步）
		/// </summary>
		/// <param name="validateFunc">异步数据验证委托</param>
		/// <returns>当前实例</returns>
		public async Task<ChainExecutor<TData>> ValidateDataAsync(Func<TData, Task<bool>> validateFunc)
		{
			if (_hasError || validateFunc == null) return this;

			try
			{
				var isValid = await validateFunc.Invoke(_innerData);
				if (!isValid)
				{
					throw new CustomValidationException("异步数据验证失败：数据不符合业务规则");
				}
				RecordLog(nameof(ValidateDataAsync), "异步数据验证通过");
			}
			catch (CustomValidationException ex)
			{
				_hasError = true;
				_innerErrorMessage = $"异步数据验证失败：{ex.Message}";
				RecordLog(nameof(ValidateDataAsync), _innerErrorMessage, false);
			}
			catch (Exception ex)
			{
				_hasError = true;
				_innerErrorMessage = $"异步数据验证异常（非验证类错误）：{ex.Message}";
				RecordLog(nameof(ValidateDataAsync), _innerErrorMessage, false);
			}

			return this;
		}
		#endregion

		#region 注释方法 无实质作用只是方便阅读

		/// <summary>
		/// 添加注释（无实质作用，仅用于链式调用中添加注释，提升代码可读性）
		/// </summary>
		/// <param name="comment"></param>
		/// <returns></returns>
		public ChainExecutor<TData> Comment(string comment)
		{
			return this;
		}

		/// <summary>
		/// 添加注释（异步版，无实质作用，仅用于链式调用中添加注释，提升代码可读性）
		/// </summary>
		/// <param name="comment"></param>
		/// <returns></returns>
		public async Task<ChainExecutor<TData>> CommentAsync(string comment)
		{
			return await Task.FromResult(this);
		}
		#endregion

		#region 最终执行方法
		/// <summary>
		/// 同步最终执行
		/// </summary>
		/// <returns>泛型执行结果模型</returns>
		public ChainExecuteResult<TData> Execute()
		{
			if (_hasError)
			{
				return ChainExecuteResult<TData>.Fail(_innerErrorMessage, _executeLogs);
			}

			return ChainExecuteResult<TData>.Success(_innerData, _executeLogs);
		}

		/// <summary>
		/// 异步最终执行
		/// </summary>
		/// <returns>泛型执行结果模型</returns>
		public async Task<ChainExecuteResult<TData>> ExecuteAsync()
		{
			await Task.CompletedTask;
			return Execute();
		}
		#endregion
	}
	#endregion
}