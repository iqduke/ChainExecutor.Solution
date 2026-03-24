using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ChainExecutor.NetCoreApp
{
	#region 自定义验证异常
	public class CustomValidationException : Exception
	{
		public CustomValidationException() : base() { }
		public CustomValidationException(string message) : base(message) { }
		public CustomValidationException(string message, Exception innerException) : base(message, innerException) { }
	}
	#endregion

	#region 泛型执行结果模型
	public class ChainExecuteResult<TData> where TData : class, new()
	{
		public bool IsSuccess { get; set; }
		public string ErrorMessage { get; set; } = string.Empty;
		public TData? Data { get; set; }
		public List<string> ExecuteLogs { get; set; } = new();
		public long ElapsedMilliseconds { get; set; }

		public static ChainExecuteResult<TData> Success(TData data, List<string> logs, long elapsed)
		{
			return new ChainExecuteResult<TData>
			{
				IsSuccess = true,
				Data = data,
				ExecuteLogs = logs,
				ElapsedMilliseconds = elapsed
			};
		}

		public static ChainExecuteResult<TData> Fail(string error, List<string> logs, long elapsed)
		{
			return new ChainExecuteResult<TData>
			{
				IsSuccess = false,
				ErrorMessage = error,
				ExecuteLogs = logs,
				ElapsedMilliseconds = elapsed
			};
		}
	}
	#endregion

	#region 链式执行上下文
	public class ChainContext<TData> where TData : class, new()
	{
		public TData? Data { get; set; }
		public ConcurrentDictionary<string, object> Parameters { get; set; } = new();
		public Dictionary<string, object> Items { get; set; } = new();
		public IServiceProvider? ServiceProvider { get; set; }
		public bool HasError { get; set; }
		public string? ErrorMessage { get; set; }
	}
	#endregion

	#region 企业级泛型链式执行器
	public class ChainExecutor<TData> where TData : class, new()
	{
		private readonly ChainContext<TData> _ctx = new();
		private readonly List<string> _logs = new();
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private string _caller = "UnknownCaller";

		#region 钩子委托
		public Func<ChainContext<TData>, Task>? OnBeforeExecuteAsync;
		public Func<ChainContext<TData>, Task>? OnAfterExecuteAsync;
		public Func<ChainContext<TData>, Exception?, Task>? OnErrorAsync;
		#endregion

		public ChainExecutor()
		{
			CaptureCaller();
			Log("ChainExecutor", "初始化企业级链式执行器");
		}

		#region 核心辅助
		private void CaptureCaller()
		{
			try
			{
				var frame = new StackTrace(2).GetFrame(0);
				var method = frame?.GetMethod();
				_caller = $"{method?.DeclaringType?.FullName}.{method?.Name}";
			}
			catch { }
		}

		private void Log(string method, string msg, bool ok = true)
		{
			_logs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{_caller}] [{method}] {(ok ? "OK" : "FAIL")} | {msg}");
		}

		private void SetError(string msg)
		{
			_ctx.HasError = true;
			_ctx.ErrorMessage = msg;
			Log("Error", msg, false);
		}
		#endregion

		#region 配置
		public ChainExecutor<TData> WithServiceProvider(IServiceProvider sp)
		{
			_ctx.ServiceProvider = sp;
			return this;
		}
		#endregion

		#region 初始化
		public ChainExecutor<TData> InitData(TData data)
		{
			if (_ctx.HasError) return this;
			try
			{
				_ctx.Data = data;
				Log("InitData", "模型初始化完成");
			}
			catch (Exception ex) { SetError($"InitData异常：{ex.Message}"); }
			return this;
		}

		public ChainExecutor<TData> WithParameters(ConcurrentDictionary<string, object> param)
		{
			if (_ctx.HasError) return this;
			_ctx.Parameters = param ?? new();
			Log("WithParameters", "参数初始化完成");
			return this;
		}
		#endregion

		#region 验证
		public ChainExecutor<TData> ValidateModel(Func<TData, bool> validate)
		{
			if (_ctx.HasError || validate == null) return this;
			try
			{
				bool ok = validate(_ctx.Data!);
				if (!ok) throw new CustomValidationException("模型验证不通过");
				Log("ValidateModel", "验证通过");
			}
			catch (CustomValidationException ex) { SetError(ex.Message); }
			catch (Exception ex) { SetError($"验证异常：{ex.Message}"); }
			return this;
		}

		public ChainExecutor<TData> ValidateAnnotations()
		{
			if (_ctx.HasError || _ctx.Data == null) return this;
			try
			{
				var context = new ValidationContext(_ctx.Data);
				var errors = new List<ValidationResult>();
				bool valid = Validator.TryValidateObject(_ctx.Data, context, errors, true);

				if (!valid)
				{
					var msg = string.Join(" | ", errors.Select(x => x.ErrorMessage));
					throw new CustomValidationException(msg);
				}
				Log("ValidateAnnotations", "特性验证通过");
			}
			catch (CustomValidationException ex) { SetError(ex.Message); }
			catch (Exception ex) { SetError($"注解验证失败：{ex.Message}"); }
			return this;
		}
		#endregion

		#region 同步处理
		public ChainExecutor<TData> Process(Action<ChainContext<TData>> action)
		{
			if (_ctx.HasError || action == null) return this;
			try
			{
				action(_ctx);
				Log("Process", "业务处理完成");
			}
			catch (Exception ex) { SetError($"处理失败：{ex.Message}"); }
			return this;
		}
		#endregion

		#region 异步处理
		public async Task<ChainExecutor<TData>> ProcessAsync(Func<ChainContext<TData>, Task> func)
		{
			if (_ctx.HasError || func == null) return this;
			try
			{
				await func(_ctx);
				Log("ProcessAsync", "异步业务处理完成");
			}
			catch (Exception ex) { SetError($"异步处理失败：{ex.Message}"); }
			return this;
		}
		#endregion

		#region 事务
		public ChainExecutor<TData> UseTransaction(Action<ChainContext<TData>> action)
		{
			if (_ctx.HasError) return this;
			try
			{
				action(_ctx);
				Log("UseTransaction", "事务逻辑执行成功（自动提交）");
			}
			catch (Exception ex)
			{
				SetError($"事务回滚：{ex.Message}");
			}
			return this;
		}
		#endregion

		#region 执行
		public async Task<ChainExecuteResult<TData>> ExecuteAsync()
		{
			Exception? error = null;
			try
			{
				if (OnBeforeExecuteAsync != null)
					await OnBeforeExecuteAsync.Invoke(_ctx);

				if (_ctx.HasError)
					throw new Exception(_ctx.ErrorMessage);

				return ChainExecuteResult<TData>.Success(
					_ctx.Data ?? new TData(),
					_logs,
					_stopwatch.ElapsedMilliseconds);
			}
			catch (Exception ex)
			{
				error = ex;
				SetError(ex.Message);
				return ChainExecuteResult<TData>.Fail(
					_ctx.ErrorMessage!,
					_logs,
					_stopwatch.ElapsedMilliseconds);
			}
			finally
			{
				try
				{
					if (_ctx.HasError && OnErrorAsync != null)
						await OnErrorAsync.Invoke(_ctx, error);

					if (OnAfterExecuteAsync != null)
						await OnAfterExecuteAsync.Invoke(_ctx);
				}
				catch { }
			}
		}

		public ChainExecuteResult<TData> Execute()
		{
			return ExecuteAsync().GetAwaiter().GetResult();
		}
		#endregion
	}
	#endregion
}