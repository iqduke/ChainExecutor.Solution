using ChainExecutor.Framework;

namespace ChainExecutor.Test
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {

			// 测试1：同步链式调用 + 缓存功能
			Console.WriteLine("\n===== 测试1：同步+缓存（第一次执行，缓存未命中） =====");
			await TestSyncChainWithCache("User_1");

			Console.WriteLine("\n===== 测试1：同步+缓存（第二次执行，缓存命中） =====");
			await TestSyncChainWithCache("User_1");

			// 测试2：异步链式调用 + 自定义验证异常
			Console.WriteLine("\n===== 测试2：异步+自定义验证异常 =====");
			await TestAsyncChainWithCustomException();
		}

		/// <summary>
		/// 测试同步链式调用 + 缓存
		/// </summary>
		/// <param name="cacheKey">缓存Key</param>
		static async Task TestSyncChainWithCache(string cacheKey)
		{
			var result = new ChainExecutor<User>()
				.Comment("初始化")
				.InitData(new User { Id = 1, Name = "张三", Age = 25 })
				.Comment("校验")
				.ValidateData(user => user?.Age >= 18)
				.Comment("业务处理")
				.ProcessBusiness(user =>
				{
					if (user != null)
					{
						user.Name = $"[{user.Name}] - 已实名认证（同步处理）";
						System.Threading.Thread.Sleep(300); // 模拟耗时业务
					}
				})
				.Execute();

			// 打印结果
			PrintTestResult(result);
		}

		/// <summary>
		/// 测试异步链式调用 + 自定义验证异常
		/// </summary>
		static async Task TestAsyncChainWithCustomException()
		{
			var result = await new ChainExecutor<User>()
				.CommentAsync("初始化数据").GetAwaiter().GetResult()
				.InitDataAsync(async () =>
				{
					await Task.Delay(200); // 模拟异步查询数据
					return new User { Id = 2, Name = "李四", Age = 17 }; // 年龄不足18，触发验证异常
				}).GetAwaiter().GetResult()
				.CommentAsync("校验").GetAwaiter().GetResult()
				.ValidateDataAsync(async (user) =>
				{
					await Task.Delay(100); // 模拟异步验证
					return user?.Age >= 18;
				}).GetAwaiter().GetResult()
				.ProcessBusinessAsync(async (user) =>
				{
					await Task.Delay(200); // 模拟异步更新数据
					if (user != null) user.Name = $"[{user.Name}] - 已实名认证（异步处理）";
				}).GetAwaiter().GetResult()
				.ExecuteAsync();

			// 打印结果
			PrintTestResult(result);
		}

		/// <summary>
		/// 通用测试结果打印方法
		/// </summary>
		/// <typeparam name="T">数据类型</typeparam>
		/// <param name="result">执行结果</param>
		static void PrintTestResult<T>(ChainExecuteResult<T> result) where T : class, new()
		{
			Console.WriteLine($"\n----- 结果汇总 -----");
			Console.WriteLine($"执行状态：{(result.IsSuccess ? "✅ 成功" : "❌ 失败")}");
			if (!result.IsSuccess)
			{
				Console.WriteLine($"错误信息：{result.ErrorMessage}");
				return;
			}

			// 打印业务数据（仅User类型）
			if (result.Data is User user)
			{
				Console.WriteLine($"用户ID：{user.Id}");
				Console.WriteLine($"用户姓名：{user.Name}");
				Console.WriteLine($"用户年龄：{user.Age}");
			}

			// 打印前3条日志（简化输出，如需完整日志可遍历全部）
			Console.WriteLine($"\n----- 前3条执行日志 -----");
			var logCount = Math.Min(3, result.ExecuteLogs.Count);
			for (int i = 0; i < logCount; i++)
			{
				Console.WriteLine(result.ExecuteLogs[i]);
			}
		}
	}

	public class User
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Age { get; set; }
	}
}
