﻿using Forum3.Contexts;
using Forum3.Interfaces.Services;
using Forum3.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Forum3.Controllers {
	using InputModels = Models.InputModels;
	using PageViewModels = Models.ViewModels.Boards.Pages;

	public class Boards : Controller {
		ApplicationDbContext DbContext { get; }
		BoardRepository BoardRepository { get; }
		RoleRepository RoleRepository { get; }
		AccountRepository AccountRepository { get; }
		NotificationRepository NotificationRepository { get; }
		IForumViewResult ForumViewResult { get; }

		public Boards(
			ApplicationDbContext dbContext,
			BoardRepository boardRepository,
			RoleRepository roleRepository,
			AccountRepository accountRepository,
			NotificationRepository notificationRepository,
			IForumViewResult forumViewResult
		) {
			DbContext = dbContext;
			BoardRepository = boardRepository;
			RoleRepository = roleRepository;
			AccountRepository = accountRepository;
			NotificationRepository = notificationRepository;
			ForumViewResult = forumViewResult;
		}

		[HttpGet]
		public IActionResult Index() {
			var sidebar = new Models.ViewModels.Sidebar {
				Birthdays = AccountRepository.GetBirthdaysList().ToArray(),
				OnlineUsers = AccountRepository.GetOnlineList(),
				Notifications = NotificationRepository.Index()
			};

			var viewModel = new PageViewModels.IndexPage {
				Categories = BoardRepository.CategoryIndex(true),
				Sidebar = sidebar
			};

			return ForumViewResult.ViewResult(this, viewModel);
		}

		[Authorize(Roles="Admin")]
		[HttpGet]
		public IActionResult Manage() {
			var viewModel = new PageViewModels.IndexPage {
				Categories = BoardRepository.CategoryIndex()
			};

			return ForumViewResult.ViewResult(this, viewModel);
		}

		[Authorize(Roles="Admin")]
		[HttpGet]
		public IActionResult Create() {
			var viewModel = new PageViewModels.CreatePage() {
				Categories = BoardRepository.CategoryPickList()
			};

			return ForumViewResult.ViewResult(this, viewModel);
		}

		[Authorize(Roles = "Admin")]
		[HttpPost]
		public async Task<IActionResult> Create(InputModels.CreateBoardInput input) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.AddBoard(input);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				var viewModel = new PageViewModels.CreatePage() {
					Categories = BoardRepository.CategoryPickList()
				};

				viewModel.Name = input.Name;
				viewModel.Description = input.Description;

				if (!string.IsNullOrEmpty(input.Category))
					viewModel.Categories.First(item => item.Value == input.Category).Selected = true;

				return await Task.Run(() => { return ForumViewResult.ViewResult(this, viewModel); });
			}
		}

		[Authorize(Roles="Admin")]
		[HttpGet]
		public IActionResult Edit(int id) {
			var boardRecord = BoardRepository.First(b => b.Id == id);
			var category = BoardRepository.Categories.First(item => item.Id == boardRecord.CategoryId);

			var viewModel = new PageViewModels.EditPage {
				Id = boardRecord.Id,
				Name = boardRecord.Name,
				Description = boardRecord.Description,
				Categories = BoardRepository.CategoryPickList(),
				Roles = RoleRepository.PickList(boardRecord.Id),
			};

			viewModel.Categories.First(item => item.Text == category.Name).Selected = true;

			return ForumViewResult.ViewResult(this, viewModel);
		}

		[Authorize(Roles = "Admin")]
		[HttpPost]
		public async Task<IActionResult> Edit(InputModels.EditBoardInput input) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.UpdateBoard(input);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				var boardRecord = BoardRepository.First(b => b.Id == input.Id);

				var viewModel = new PageViewModels.EditPage {
					Id = boardRecord.Id,
					Categories = BoardRepository.CategoryPickList(),
					Roles = RoleRepository.PickList(boardRecord.Id)
				};

				viewModel.Name = input.Name;
				viewModel.Description = input.Description;

				if (!string.IsNullOrEmpty(input.Category))
					viewModel.Categories.First(item => item.Value == input.Category).Selected = true;

				return await Task.Run(() => { return ForumViewResult.ViewResult(this, viewModel); });
			}
		}

		[Authorize(Roles="Admin")]
		[HttpGet]
		public async Task<IActionResult> MoveCategoryUp(int id) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.MoveCategoryUp(id);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				return await Task.Run(() => { return ForumViewResult.RedirectToReferrer(this); });
			}
		}

		[Authorize(Roles="Admin")]
		[HttpGet]
		public async Task<IActionResult> MoveBoardUp(int id) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.MoveBoardUp(id);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				return await Task.Run(() => { return ForumViewResult.RedirectToReferrer(this); });
			}
		}

		[Authorize(Roles="Admin")]
		[HttpPost]
		public async Task<IActionResult> MergeCategory(
			InputModels.MergeInput input
		) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.MergeCategory(input);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				return await Task.Run(() => { return ForumViewResult.RedirectToReferrer(this); });
			}
		}

		[Authorize(Roles="Admin")]
		[HttpPost]
		public async Task<IActionResult> MergeBoard(InputModels.MergeInput input) {
			if (ModelState.IsValid) {
				var serviceResponse = BoardRepository.MergeBoard(input);
				return await ForumViewResult.RedirectFromService(this, serviceResponse, FailureCallback);
			}

			return await FailureCallback();

			async Task<IActionResult> FailureCallback() {
				return await Task.Run(() => { return ForumViewResult.RedirectToReferrer(this); });
			}
		}
	}
}