﻿using Forum3.Annotations;
using Forum3.Contexts;
using Forum3.Errors;
using Forum3.Interfaces.Filters;
using Forum3.Interfaces.Services;
using Forum3.Middleware;
using Forum3.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage;

// REMINDER -
// Transient: created each time they are requested. This lifetime works best for lightweight, stateless services.
// Scoped: created once per request.
// Singleton: created the first time they are requested (or when ConfigureServices is run if you specify an instance there) and then every subsequent request will use the same instance.

namespace Forum3.Extensions {
	using ServiceModels = Models.ServiceModels;

	public static class ForumStartupExtensions {
		public static IApplicationBuilder UseForum(this IApplicationBuilder builder) {
			builder.UseMiddleware<HttpStatusCodeHandler>();
			builder.UseMiddleware<PageTimer>();

			return builder;
		}

		public static IServiceCollection AddForum(this IServiceCollection services, IConfiguration configuration) {
			RegisterRepositories(services, configuration);

			RegisterAzureStorage(services, configuration);

			services.Configure<ServiceModels.RecaptchaOptions>(configuration);
			services.AddTransient<IRecaptchaValidator, RecaptchaValidator>();
			services.AddTransient<ValidateRecaptchaActionFilter>();

			services.Configure<ServiceModels.EmailSenderOptions>(configuration);
			services.AddTransient<IEmailSender, EmailSender>();

			services.AddTransient<IImageStore, ImageStore>();
			services.AddTransient<IForumViewResult, ForumViewResult>();

			services.AddScoped<UserContext>();

			services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
			services.AddSingleton<IUrlHelperFactory, UrlHelperFactory>();

			services.AddSingleton((serviceProvider) => {
				return BBCParserFactory.GetParser();
			});

			return services;
		}

		static void RegisterRepositories(IServiceCollection services, IConfiguration configuration) {
			services.AddScoped<Repositories.AccountRepository>();
			services.AddScoped<Repositories.BoardRepository>();
			services.AddScoped<Repositories.MessageRepository>();
			services.AddScoped<Repositories.NotificationRepository>();
			services.AddScoped<Repositories.PinRepository>();
			services.AddScoped<Repositories.RoleRepository>();
			services.AddScoped<Repositories.SettingsRepository>();
			services.AddScoped<Repositories.SmileyRepository>();
			services.AddScoped<Repositories.TopicRepository>();
		}

		static void RegisterAzureStorage(IServiceCollection services, IConfiguration configuration) {
			services.AddScoped((serviceProvider) => {
				var storageConnectionString = configuration[Constants.Keys.StorageConnection];

				if (string.IsNullOrEmpty(storageConnectionString))
					storageConnectionString = configuration.GetConnectionString(Constants.Keys.StorageConnection);

				if (string.IsNullOrEmpty(storageConnectionString))
					throw new HttpInternalServerError("No storage connection string found.");

				var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

				return storageAccount.CreateCloudBlobClient();
			});
		}
	}
}