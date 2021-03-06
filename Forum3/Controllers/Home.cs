﻿using Forum3.Contexts;
using Forum3.Interfaces.Services;
using Forum3.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Forum3.Controllers {
	using ViewModels = Models.ViewModels;

	public class Home : Controller {
		UserContext UserContext { get; }
		SettingsRepository SettingsRepository { get; }
		IForumViewResult ForumViewResult { get; }

		public Home(
			UserContext userContext,
			IForumViewResult forumViewResult,
			SettingsRepository settingsRepository
		) {
			UserContext = userContext;
			ForumViewResult = forumViewResult;
			SettingsRepository = settingsRepository;
		}

		[HttpGet]
		public IActionResult FrontPage() {
			var frontpage = SettingsRepository.FrontPage();

			switch (frontpage) {
				default:
				case "Board List":
					return RedirectToAction(nameof(Boards.Index), nameof(Boards));

				case "All Topics":
					return RedirectToAction(nameof(Topics.Index), nameof(Topics), new { id = 0 });

				case "Unread Topics":
					return RedirectToAction(nameof(Topics.Index), nameof(Topics), new { id = 0, unread = 1 });
			}
		}

		[AllowAnonymous]
		public IActionResult Error() {
			var viewModel = new ViewModels.Error {
				RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
			};

			return ForumViewResult.ViewResult(this, viewModel);
		}
	}
}