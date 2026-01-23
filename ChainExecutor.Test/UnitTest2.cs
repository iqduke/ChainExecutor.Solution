using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChainExecutor.NetCoreApp;

namespace ChainExecutor.Test
{
	public class UnitTest2
    {
		[Fact]
		public async Task Test2()
		{
			Console.WriteLine("\n===== 测试2：同步+缓存（第二次执行，缓存命中） =====");
			await TestSyncChainWithCache2();
		}


		/// <summary>
		/// 测试同步链式调用 + 缓存
		/// </summary>
		static async Task TestSyncChainWithCache2()
		{

			var param = new ConcurrentDictionary<string, object>();
			param.AddOrUpdate("Id", 1, (k, v) => 1);

			var result = new ChainExecutor<User>()
				.Comment("初始化")
				.InitData(new User { Id = 1, Name = "张三", Age = 25 })
				.InitData(param)
				.Comment("校验")
				.ValidateData(user => user?.Age >= 18)
				.Comment("业务处理")
				.ProcessBusiness((user, pa) =>
				{
					if (user != null)
					{
						user.Name = $"[{user.Name}] - 已实名认证（同步处理）";
						System.Threading.Thread.Sleep(300); // 模拟耗时业务
					}

					Console.Write(pa.GetOrAdd("Id", -1));
				})
				.Execute();

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
}
