﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DNIC.Erechtheion.Models;
using DNIC.Erechtheion.Services;
using DNIC.Erechtheion.Core;
using DNIC.Erechtheion.Core.Configuration;
using System.IdentityModel.Tokens.Jwt;
using DNIC.Erechtheion.EntityFrameworkCore;
using DNIC.Erechtheion.Domain;
using Microsoft.Extensions.Logging;
using DNIC.Erechtheion.Application.Service;
using Serilog;
using Serilog.Events;

namespace DNIC.Erechtheion
{
	public class Startup
	{
		public IErechtheionConfiguration ErechtheionConfiguration { get; }
		public IHostingEnvironment Environment { get; }

		public Startup(IHostingEnvironment env)
		{
			Environment = env;

			var configurationFile = env.IsDevelopment() ? $"appsettings.{env.EnvironmentName}.json" : $"appsettings.json";
			IConfigurationBuilder builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile(configurationFile, optional: true, reloadOnChange: true)
				.AddEnvironmentVariables();
			ErechtheionConfiguration = new ErechtheionConfiguration(builder.Build());

			// 配置 Serilog
			Log.Logger = new LoggerConfiguration()
				.Enrich.FromLogContext()
				.ReadFrom.Configuration(ErechtheionConfiguration.Configuration)
				.WriteTo.Console().WriteTo.File("DNIC.Erechtheion.log")
				.CreateLogger();
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.AddEntityFrameworkSqlServer()
			.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ErechtheionConfiguration.ConnectionString, b => b.UseRowNumberForPaging()));

			services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ErechtheionConfiguration.ConnectionString));

			// Add application services.
			services.AddTransient<IEmailSender, EmailSender>();

			services.AddResponseCompression();
			services.AddMvc();

			// 重新注册
			services.AddSingleton(ErechtheionConfiguration.Configuration);

			// 注册系统配置
			services.AddSingleton(ErechtheionConfiguration);

			DependencyInjectionConfig.Inject(services);

			services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

			// 如果没有配置全局登录系统, 则使用默认注册和登录
			if (!string.IsNullOrWhiteSpace(ErechtheionConfiguration.AccountCenter))
			{
				JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

				services.AddAuthentication("DNIC")
					.AddCookie("DNIC")
					.AddIdentityServerAuthentication(options =>
					{
						options.Authority = "http://localhost:5000";
						options.RequireHttpsMetadata = false;
						options.ApiName = "DNIC";
					});
			}
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(ErechtheionConfiguration.Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();
			loggerFactory.AddSerilog();

			if (env.IsDevelopment())
			{
				app.UseBrowserLink();
				app.UseDeveloperExceptionPage();
				app.UseDatabaseErrorPage();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}

			app.UseStaticFiles();

			app.UseAuthentication();

			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});

			AutoMapperConfiguration.CreateMap();
		}
	}
}
