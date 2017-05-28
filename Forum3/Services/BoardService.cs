﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Forum3.Controllers;
using Forum3.Data;
using Forum3.Helpers;
using Forum3.Models.ServiceModels;
using DataModels = Forum3.Models.DataModels;
using InputModels = Forum3.Models.InputModels;
using PageViewModels = Forum3.Models.ViewModels.Boards.Pages;
using ItemViewModels = Forum3.Models.ViewModels.Boards.Items;

namespace Forum3.Services {
	public class BoardService {
		ApplicationDbContext DbContext { get; }
		SiteSettingsService SiteSettingsService { get; }
		ContextUser ContextUser { get; }
		IUrlHelper UrlHelper { get; }

		public BoardService(
			ApplicationDbContext dbContext,
			SiteSettingsService siteSettingsService,
			ContextUserFactory contextUserFactory,
			IActionContextAccessor actionContextAccessor,
			IUrlHelperFactory urlHelperFactory
		) {
			DbContext = dbContext;
			SiteSettingsService = siteSettingsService;
			ContextUser = contextUserFactory.GetContextUser();
			UrlHelper = urlHelperFactory.GetUrlHelper(actionContextAccessor.ActionContext);
		}

		public async Task<PageViewModels.IndexPage> IndexPage() {
			var birthdays = GetBirthdays();
			var onlineUsers = GetOnlineUsers();

			await Task.WhenAll(new Task[] {
				birthdays,
				onlineUsers
			});

			var viewModel = new PageViewModels.IndexPage {
				Birthdays = birthdays.Result.ToArray(),
				Categories = await GetCategories(),
				OnlineUsers = onlineUsers.Result
			};

			return viewModel;
		}

		public async Task<PageViewModels.IndexPage> ManagePage() {
			var viewModel = new PageViewModels.IndexPage {
				Categories = await GetCategories()
			};

			return viewModel;
		}

		public async Task<PageViewModels.CreatePage> CreatePage(InputModels.CreateBoardInput input = null) {
			var viewModel = new PageViewModels.CreatePage() {
				Categories = await GetCategoryPickList()
			};

			if (input != null) {
				viewModel.Name = input.Name;
				viewModel.Description = input.Description;

				if (!string.IsNullOrEmpty(input.Category))
					viewModel.Categories.First(item => item.Value == input.Category).Selected = true;
			}

			return viewModel;
		}

		public async Task<PageViewModels.EditPage> EditPage(int boardId, InputModels.EditBoardInput input = null) {
			var record = await DbContext.Boards.SingleOrDefaultAsync(b => b.Id == boardId);

			if (record == null)
				throw new Exception($"A record does not exist with ID '{boardId}'");

			var viewModel = new PageViewModels.EditPage() {
				Id = record.Id,
				Categories = await GetCategoryPickList()
			};

			if (input != null) {
				viewModel.Name = input.Name;
				viewModel.Description = input.Description;

				if (!string.IsNullOrEmpty(input.Category))
					viewModel.Categories.First(item => item.Value == input.Category).Selected = true;
			}
			else {
				var category = await DbContext.Categories.FindAsync(record.CategoryId);

				viewModel.Name = record.Name;
				viewModel.Description = record.Description;
				viewModel.Categories.First(item => item.Text == category.Name).Selected = true;
			}

			return viewModel;
		}

		public async Task<ServiceResponse> Create(InputModels.CreateBoardInput input) {
			var serviceResponse = new ServiceResponse();

			if (await DbContext.Boards.AnyAsync(b => b.Name == input.Name))
				serviceResponse.ModelErrors.Add(nameof(input.Name), "A board with that name already exists");

			DataModels.Category categoryRecord = null;

			if (!string.IsNullOrEmpty(input.NewCategory))
				input.NewCategory = input.NewCategory.Trim();

			if (!string.IsNullOrEmpty(input.NewCategory)) {
				categoryRecord = await DbContext.Categories.SingleOrDefaultAsync(c => c.Name == input.NewCategory);

				if (categoryRecord == null) {
					var displayOrder = await DbContext.Categories.MaxAsync(c => c.DisplayOrder);

					categoryRecord = new DataModels.Category {
						Name = input.NewCategory,
						DisplayOrder = displayOrder + 1
					};

					await DbContext.Categories.AddAsync(categoryRecord);
				}
			}
			else {
				try {
					var categoryId = Convert.ToInt32(input.Category);
					categoryRecord = await DbContext.Categories.SingleOrDefaultAsync(c => c.Id == categoryId);

					if (categoryRecord == null)
						serviceResponse.ModelErrors.Add(nameof(input.Category), "No category was found with this ID.");
				}
				catch (FormatException) {
					serviceResponse.ModelErrors.Add(nameof(input.Category), "Invalid category ID");
				}
			}

			if (!string.IsNullOrEmpty(input.Name))
				input.Name = input.Name.Trim();

			if (string.IsNullOrEmpty(input.Name))
				serviceResponse.ModelErrors.Add(nameof(input.Name), "Name is a required field.");

			if (!string.IsNullOrEmpty(input.Description))
				input.Description = input.Description.Trim();

			var existingRecord = await DbContext.Boards.SingleOrDefaultAsync(b => b.Name == input.Name);

			if (existingRecord != null)
				serviceResponse.ModelErrors.Add(nameof(input.Name), "A board with that name already exists");

			if (serviceResponse.ModelErrors.Any())
				return serviceResponse;

			await DbContext.SaveChangesAsync();

			var record = new DataModels.Board {
				Name = input.Name,
				Description = input.Description,
				CategoryId = categoryRecord.Id
			};

			await DbContext.Boards.AddAsync(record);
			await DbContext.SaveChangesAsync();

			serviceResponse.RedirectPath = UrlHelper.Action(nameof(Boards.Manage), nameof(Boards), new { id = record.Id });

			return serviceResponse;
		}

		public async Task<ServiceResponse> Edit(InputModels.EditBoardInput input) {
			var serviceResponse = new ServiceResponse();

			var record = await DbContext.Boards.SingleOrDefaultAsync(b => b.Id == input.Id);

			if (record == null)
				serviceResponse.ModelErrors.Add(string.Empty, $"A record does not exist with ID '{input.Id}'");

			DataModels.Category newCategoryRecord = null;

			if (!string.IsNullOrEmpty(input.NewCategory))
				input.NewCategory = input.NewCategory.Trim();

			if (!string.IsNullOrEmpty(input.NewCategory)) {
				newCategoryRecord = await DbContext.Categories.SingleOrDefaultAsync(c => c.Name == input.NewCategory);

				if (newCategoryRecord == null) {
					var displayOrder = await DbContext.Categories.MaxAsync(c => c.DisplayOrder);

					newCategoryRecord = new DataModels.Category {
						Name = input.NewCategory,
						DisplayOrder = displayOrder + 1
					};

					await DbContext.Categories.AddAsync(newCategoryRecord);
				}
			}
			else {
				try {
					var newCategoryId = Convert.ToInt32(input.Category);
					newCategoryRecord = await DbContext.Categories.SingleOrDefaultAsync(c => c.Id == newCategoryId);

					if (newCategoryRecord == null)
						serviceResponse.ModelErrors.Add(nameof(input.Category), "No category was found with this ID.");
				}
				catch (FormatException) {
					serviceResponse.ModelErrors.Add(nameof(input.Category), "Invalid category ID");
				}
			}

			if (!string.IsNullOrEmpty(input.Name))
				input.Name = input.Name.Trim();

			if (string.IsNullOrEmpty(input.Name))
				serviceResponse.ModelErrors.Add(nameof(input.Name), "Name is a required field.");

			if (!string.IsNullOrEmpty(input.Description))
				input.Description = input.Description.Trim();

			if (serviceResponse.ModelErrors.Any())
				return serviceResponse;

			record.Name = input.Name;
			record.Description = input.Description;

			var oldCategoryId = -1;

			if (record.CategoryId != newCategoryRecord.Id) {
				var categoryBoards = await DbContext.Boards.Where(r => r.CategoryId == record.CategoryId).ToListAsync();

				if (categoryBoards.Count() <= 1)
					oldCategoryId = record.CategoryId;

				record.CategoryId = newCategoryRecord.Id;
			}

			DbContext.Entry(record).State = EntityState.Modified;
			await DbContext.SaveChangesAsync();

			if (oldCategoryId >= 0) {
				var oldCategoryRecord = DbContext.Categories.Find(oldCategoryId);
				DbContext.Categories.Remove(oldCategoryRecord);
				await DbContext.SaveChangesAsync();
			}

			serviceResponse.RedirectPath = UrlHelper.Action(nameof(Boards.Manage), nameof(Boards), new { id = record.Id });

			return serviceResponse;
		}

		public async Task<ServiceResponse> MoveCategoryUp(int id) {
			var serviceResponse = new ServiceResponse();

			var targetCategory = DbContext.Categories.FirstOrDefault(b => b.Id == id);

			if (targetCategory == null) {
				serviceResponse.ModelErrors.Add(string.Empty, "No category found with that ID.");
				return serviceResponse;
			}

			if (targetCategory.DisplayOrder > 1) {
				var displacedCategory = DbContext.Categories.First(b => b.DisplayOrder == targetCategory.DisplayOrder - 1);

				displacedCategory.DisplayOrder++;
				DbContext.Entry(displacedCategory).State = EntityState.Modified;

				targetCategory.DisplayOrder--;
				DbContext.Entry(targetCategory).State = EntityState.Modified;

				await DbContext.SaveChangesAsync();
			}

			return serviceResponse;
		}

		public async Task<ServiceResponse> MoveBoardUp(int id) {
			var serviceResponse = new ServiceResponse();

			var targetBoard = await DbContext.Boards.FirstOrDefaultAsync(b => b.Id == id);

			if (targetBoard == null) {
				serviceResponse.ModelErrors.Add(string.Empty, "No board found with that ID.");
				return serviceResponse;
			}

			var categoryBoards = await DbContext.Boards.Where(b => b.CategoryId == targetBoard.CategoryId).OrderBy(b => b.DisplayOrder).ToListAsync();

			var currentIndex = 1;

			foreach (var board in categoryBoards) {
				board.DisplayOrder = currentIndex++;
				DbContext.Entry(board).State = EntityState.Modified;
			}

			await DbContext.SaveChangesAsync();

			targetBoard = categoryBoards.First(b => b.Id == id);

			if (targetBoard.DisplayOrder > 1) {
				var displacedBoard = categoryBoards.FirstOrDefault(b => b.DisplayOrder == targetBoard.DisplayOrder - 1);

				if (displacedBoard != null) {
					displacedBoard.DisplayOrder++;
					DbContext.Entry(displacedBoard).State = EntityState.Modified;
				}

				targetBoard.DisplayOrder--;
				DbContext.Entry(targetBoard).State = EntityState.Modified;

				await DbContext.SaveChangesAsync();
			}
			else
				targetBoard.DisplayOrder = 2;

			return serviceResponse;
		}

		public async Task<ServiceResponse> MergeCategory(InputModels.Merge input) {
			var serviceResponse = new ServiceResponse();

			var fromCategory = await DbContext.Categories.SingleOrDefaultAsync(b => b.Id == input.FromId);
			var toCategory = await DbContext.Categories.SingleOrDefaultAsync(b => b.Id == input.ToId);

			if (fromCategory == null)
				serviceResponse.ModelErrors.Add(string.Empty, $"A record does not exist with ID '{input.FromId}'");

			if (toCategory == null)
				serviceResponse.ModelErrors.Add(string.Empty, $"A record does not exist with ID '{input.ToId}'");

			if (serviceResponse.ModelErrors.Any())
				return serviceResponse;

			var displacedBoards = await DbContext.Boards.Where(b => b.CategoryId == fromCategory.Id).ToListAsync();

			foreach (var displacedBoard in displacedBoards) {
				displacedBoard.CategoryId = toCategory.Id;
				DbContext.Entry(displacedBoard).State = EntityState.Modified;
			}

			await DbContext.SaveChangesAsync();

			DbContext.Categories.Remove(fromCategory);

			await DbContext.SaveChangesAsync();

			return serviceResponse;
		}

		public async Task<ServiceResponse> MergeBoard(InputModels.Merge input) {
			var serviceResponse = new ServiceResponse();

			var fromBoard = await DbContext.Boards.SingleOrDefaultAsync(b => b.Id == input.FromId);
			var toBoard = await DbContext.Boards.SingleOrDefaultAsync(b => b.Id == input.ToId);

			if (fromBoard == null)
				serviceResponse.ModelErrors.Add(string.Empty, $"A record does not exist with ID '{input.FromId}'");

			if (toBoard == null)
				serviceResponse.ModelErrors.Add(string.Empty, $"A record does not exist with ID '{input.ToId}'");

			if (serviceResponse.ModelErrors.Any())
				return serviceResponse;

			var messageBoards = await DbContext.MessageBoards.Where(m => m.BoardId == fromBoard.Id).ToListAsync();

			// Reassign messages to new board
			foreach (var messageBoard in messageBoards) {
				messageBoard.BoardId = toBoard.Id;
				DbContext.Entry(messageBoard).State = EntityState.Modified;
			}

			await DbContext.SaveChangesAsync();

			var categoryId = fromBoard.CategoryId;

			// Delete the board
			DbContext.Boards.Remove(fromBoard);

			await DbContext.SaveChangesAsync();

			// Remove the category if empty
			if (!await DbContext.Boards.AnyAsync(b => b.CategoryId == categoryId)) {
				var categoryRecord = await DbContext.Categories.SingleOrDefaultAsync(c => c.Id == categoryId);

				DbContext.Categories.Remove(categoryRecord);

				await DbContext.SaveChangesAsync();
			}

			return serviceResponse;
		}

		async Task<List<SelectListItem>> GetCategoryPickList(List<SelectListItem> pickList = null) {
			if (pickList == null)
				pickList = new List<SelectListItem>();

			var categoryRecords = await DbContext.Categories.OrderBy(r => r.DisplayOrder).ToListAsync();

			foreach (var categoryRecord in categoryRecords) {
				pickList.Add(new SelectListItem() {
					Text = categoryRecord.Name,
					Value = categoryRecord.Id.ToString()
				});
			}

			return pickList;
		}

		async Task<List<ItemViewModels.IndexCategory>> GetCategories(int? targetBoard = null) {
			var categoryRecords = await DbContext.Categories.OrderBy(r => r.DisplayOrder).ToListAsync();
			var boardRecords = await DbContext.Boards.OrderBy(r => r.DisplayOrder).ToListAsync();

			var indexCategories = new List<ItemViewModels.IndexCategory>();

			foreach (var categoryRecord in categoryRecords) {
				var indexCategory = new ItemViewModels.IndexCategory {
					Id = categoryRecord.Id,
					Name = categoryRecord.Name,
					DisplayOrder = categoryRecord.DisplayOrder
				};

				foreach (var boardRecord in boardRecords.Where(r => r.CategoryId == categoryRecord.Id)) {
					var indexBoard = GetIndexBoard(targetBoard, boardRecord);

					// TODO check board roles here

					indexCategory.Boards.Add(indexBoard);
				}

				// Don't index the category if there's no boards available to the user
				if (indexCategory.Boards.Any())
					indexCategories.Add(indexCategory);
			}

			return indexCategories;
		}

		ItemViewModels.IndexBoard GetIndexBoard(int? targetBoard, DataModels.Board boardRecord) {
			var indexBoard = new ItemViewModels.IndexBoard {
				Id = boardRecord.Id,
				Name = boardRecord.Name,
				Description = boardRecord.Description,
				DisplayOrder = boardRecord.DisplayOrder,
				Unread = false,
				Selected = targetBoard != null && targetBoard == boardRecord.Id,
			};

			if (boardRecord.LastMessageId != null) {
				var lastMessage = DbContext.Messages.Find(boardRecord.LastMessageId);

				if (lastMessage != null) {
					indexBoard.LastMessage = new Models.ViewModels.Topics.Items.MessagePreview {
						Id = lastMessage.Id,
						ShortPreview = lastMessage.ShortPreview,
						LastReplyByName = lastMessage.LastReplyByName,
						LastReplyId = lastMessage.LastReplyId,
						LastReplyPosted = lastMessage.LastReplyPosted.ToPassedTimeString()
					};
				}
			}

			return indexBoard;
		}

		async Task<List<ItemViewModels.OnlineUser>> GetOnlineUsers() {
			var onlineTimeLimitSetting = SiteSettingsService.GetInt(Constants.SiteSettings.OnlineTimeLimit);

			if (onlineTimeLimitSetting == 0)
				onlineTimeLimitSetting = Constants.Defaults.OnlineTimeLimit;

			if (onlineTimeLimitSetting > 0)
				onlineTimeLimitSetting *= -1;

			var onlineTimeLimit = DateTime.Now.AddMinutes(onlineTimeLimitSetting);
			var onlineTodayTimeLimit = DateTime.Now.AddMinutes(-10080);

			var onlineUsers = await (from user in DbContext.Users
									 where user.LastOnline >= onlineTodayTimeLimit
									 orderby user.LastOnline descending
									 select new ItemViewModels.OnlineUser {
										 Id = user.Id,
										 Name = user.DisplayName,
										 Online = user.LastOnline >= onlineTimeLimit,
										 LastOnline = user.LastOnline
									 }).ToListAsync();

			foreach (var onlineUser in onlineUsers)
				onlineUser.LastOnlineString = onlineUser.LastOnline.ToPassedTimeString();

			return onlineUsers;
		}

		async Task<List<string>> GetBirthdays() {
			var todayBirthdayNames = new List<string>();

			var birthdays = await DbContext.Users.Select(u => new Birthday {
				Date = u.Birthday,
				DisplayName = u.DisplayName
			}).ToListAsync();

			if (birthdays.Any()) {
				var todayBirthdays = birthdays.Where(u => new DateTime(DateTime.Now.Year, u.Date.Month, u.Date.Day).Date == DateTime.Now.Date);

				foreach (var item in todayBirthdays) {
					var now = DateTime.Today;
					var age = now.Year - item.Date.Year;

					if (item.Date > now.AddYears(-age))
						age--;

					todayBirthdayNames.Add($"{item.DisplayName} ({age})");
				}
			}

			return todayBirthdayNames;
		}

		class Birthday {
			public string DisplayName { get; set; }
			public DateTime Date { get; set; }
		}
	}
}