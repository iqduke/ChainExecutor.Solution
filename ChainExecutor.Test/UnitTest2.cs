using ChainExecutor.NetCoreApp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;

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
		}
	}

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
}
