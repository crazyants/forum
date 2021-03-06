﻿using Forum3.Contexts;
using Forum3.Controllers;
using Forum3.Errors;
using Forum3.Extensions;
using Forum3.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Forum3.Repositories {
	using DataModels = Models.DataModels;
	using InputModels = Models.InputModels;
	using ItemViewModels = Models.ViewModels.Boards.Items;
	using ServiceModels = Models.ServiceModels;

	public class AccountRepository : Repository<DataModels.ApplicationUser> {
		public bool IsAuthenticated => UserContext.IsAuthenticated;

		ApplicationDbContext DbContext { get; }
		UserContext UserContext { get; }
		SettingsRepository SettingsRepository { get; }
		UserManager<DataModels.ApplicationUser> UserManager { get; }
		SignInManager<DataModels.ApplicationUser> SignInManager { get; }
		IHttpContextAccessor HttpContextAccessor { get; }
		IUrlHelper UrlHelper { get; }
		IEmailSender EmailSender { get; }
		IImageStore ImageStore { get; }

		public AccountRepository(
			ApplicationDbContext dbContext,
			UserContext userContext,
			SettingsRepository settingsRepository,
			UserManager<DataModels.ApplicationUser> userManager,
			SignInManager<DataModels.ApplicationUser> signInManager,
			IHttpContextAccessor httpContextAccessor,
			IActionContextAccessor actionContextAccessor,
			IUrlHelperFactory urlHelperFactory,
			IEmailSender emailSender,
			IImageStore imageStore,
			ILogger<AccountRepository> log
		) : base(log) {
			DbContext = dbContext;
			UserContext = userContext;

			SettingsRepository = settingsRepository;

			UserManager = userManager;
			SignInManager = signInManager;

			HttpContextAccessor = httpContextAccessor;
			UrlHelper = urlHelperFactory.GetUrlHelper(actionContextAccessor.ActionContext);

			EmailSender = emailSender;
			ImageStore = imageStore;
		}

		public List<string> GetBirthdaysList() {
			var todayBirthdayNames = new List<string>();

			var birthdays = Records.Select(u => new {
				u.Birthday,
				u.DisplayName
			}).ToList();

			if (birthdays.Any()) {
				var todayBirthdays = birthdays.Where(u => new DateTime(DateTime.Now.Year, u.Birthday.Month, u.Birthday.Day).Date == DateTime.Now.Date);

				foreach (var item in todayBirthdays) {
					var now = DateTime.Today;
					var age = now.Year - item.Birthday.Year;

					if (item.Birthday > now.AddYears(-age))
						age--;

					todayBirthdayNames.Add($"{item.DisplayName} ({age})");
				}
			}

			return todayBirthdayNames;
		}

		public List<ItemViewModels.OnlineUser> GetOnlineList() {
			var onlineTimeLimitSetting = SettingsRepository.OnlineTimeLimit();
			onlineTimeLimitSetting *= -1;

			var onlineTimeLimit = DateTime.Now.AddMinutes(onlineTimeLimitSetting);
			var onlineTodayTimeLimit = DateTime.Now.AddMinutes(-10080);

			var onlineUsersQuery = from user in Records
								   where user.LastOnline >= onlineTodayTimeLimit
								   orderby user.LastOnline descending
								   select new ItemViewModels.OnlineUser {
									   Id = user.Id,
									   Name = user.DisplayName,
									   Online = user.LastOnline >= onlineTimeLimit,
									   LastOnline = user.LastOnline
								   };

			var onlineUsers = onlineUsersQuery.ToList();

			foreach (var onlineUser in onlineUsers)
				onlineUser.LastOnlineString = onlineUser.LastOnline.ToPassedTimeString();

			return onlineUsers;
		}

		public async Task<ServiceModels.ServiceResponse> Login(InputModels.LoginInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var result = await SignInManager.PasswordSignInAsync(input.Email, input.Password, input.RememberMe, lockoutOnFailure: false);

			if (result.IsLockedOut) {
				Log.LogWarning($"User account locked out '{input.Email}'.");
				serviceResponse.RedirectPath = UrlHelper.Action(nameof(Account.Lockout), nameof(Account));
			}
			else if (!result.Succeeded) {
				Log.LogWarning($"Invalid login attempt for account '{input.Email}'.");
				serviceResponse.Error("Invalid login attempt.");
			}
			else
				Log.LogInformation($"User logged in '{input.Email}'.");

			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> UpdateAccount(InputModels.UpdateAccountInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			if (!await UserManager.CheckPasswordAsync(UserContext.ApplicationUser, input.Password)) {
				var message = $"Invalid password for '{input.DisplayName}'.";
				serviceResponse.Error(nameof(InputModels.UpdateAccountInput.Password), message);
				Log.LogWarning(message);
			}

			var userRecord = await UserManager.FindByIdAsync(input.Id);

			if (userRecord is null) {
				var message = $"No user record found for '{input.DisplayName}'.";
				serviceResponse.Error(message);
				Log.LogCritical(message);
			}

			CanEdit(userRecord.Id);

			if (userRecord is null) {
				var message = $"No user account found for '{input.DisplayName}'.";
				serviceResponse.Error(message);
				Log.LogCritical(message);
			}

			if (!serviceResponse.Success)
				return serviceResponse;

			if (input.DisplayName != userRecord.DisplayName) {
				userRecord.DisplayName = input.DisplayName;
				DbContext.Update(userRecord);

				Log.LogInformation($"Display name was modified by '{UserContext.ApplicationUser.DisplayName}' for account '{userRecord.DisplayName}'.");
			}

			var birthday = new DateTime(input.BirthdayYear, input.BirthdayMonth, input.BirthdayDay);

			if (birthday != userRecord.Birthday) {
				userRecord.Birthday = birthday;
				DbContext.Update(userRecord);

				Log.LogInformation($"Birthday was modified by '{UserContext.ApplicationUser.DisplayName}' for account '{userRecord.DisplayName}'.");
			}

			DbContext.SaveChanges();

			if (input.NewEmail != userRecord.Email) {
				serviceResponse.RedirectPath = UrlHelper.Action(nameof(Account.Details), nameof(Account), new { id = input.DisplayName });

				var identityResult = await UserManager.SetEmailAsync(userRecord, input.NewEmail);

				if (!identityResult.Succeeded) {
					foreach (var error in identityResult.Errors) {
						Log.LogError($"Error modifying email by '{UserContext.ApplicationUser.DisplayName}' from '{userRecord.Email}' to '{input.NewEmail}'. Message: {error.Description}");
						serviceResponse.Error(error.Description);
					}

					return serviceResponse;
				}

				identityResult = await UserManager.SetUserNameAsync(userRecord, input.NewEmail);

				if (!identityResult.Succeeded) {
					foreach (var error in identityResult.Errors) {
						Log.LogError($"Error modifying username by '{UserContext.ApplicationUser.DisplayName}' from '{userRecord.Email}' to '{input.NewEmail}'. Message: {error.Description}");
						serviceResponse.Error(error.Description);
					}

					return serviceResponse;
				}

				Log.LogInformation($"Email address was modified by '{UserContext.ApplicationUser.DisplayName}' from '{userRecord.Email}' to '{input.NewEmail}'.");

				var code = await UserManager.GenerateEmailConfirmationTokenAsync(userRecord);

				if (EmailSender.Ready) {
					var callbackUrl = EmailConfirmationLink(userRecord.Id, code);

					await EmailSender.SendEmailConfirmationAsync(input.NewEmail, callbackUrl);

					if (userRecord.Id == UserContext.ApplicationUser.Id)
						await SignOut();
				}
				else {
					identityResult = await UserManager.ConfirmEmailAsync(userRecord, code);

					if (!identityResult.Succeeded) {
						foreach (var error in identityResult.Errors) {
							Log.LogError($"Error confirming '{userRecord.Email}'. Message: {error.Description}");
							serviceResponse.Error(error.Description);
						}
					}
					else
						Log.LogInformation($"User confirmed email '{userRecord.Email}'.");
				}

				return serviceResponse;
			}

			SettingsRepository.UpdateUserSettings(input);

			if (!string.IsNullOrEmpty(input.NewPassword) && input.Password != input.NewPassword && UserContext.ApplicationUser.Id == input.Id) {
				var identityResult = await UserManager.ChangePasswordAsync(userRecord, input.Password, input.NewPassword);

				if (!identityResult.Succeeded) {
					foreach (var error in identityResult.Errors) {
						Log.LogError($"Error modifying password by '{UserContext.ApplicationUser.DisplayName}' for '{userRecord.DisplayName}'. Message: {error.Description}");
						serviceResponse.Error(nameof(InputModels.UpdateAccountInput.NewPassword), error.Description);
					}
				}
				else if (userRecord.Id == UserContext.ApplicationUser.Id) {
					Log.LogInformation($"Password was modified by '{UserContext.ApplicationUser.DisplayName}' for '{userRecord.DisplayName}'.");
					await SignOut();
					serviceResponse.RedirectPath = UrlHelper.Action(nameof(Login));
					return serviceResponse;
				}
			}

			if (serviceResponse.Success)
				serviceResponse.RedirectPath = UrlHelper.Action(nameof(Account.Details), nameof(Account), new { id = input.DisplayName });

			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> UpdateAvatar(InputModels.UpdateAvatarInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var userRecord = await UserManager.FindByIdAsync(input.Id);

			if (userRecord is null) {
				var message = $"No user record found for '{input.Id}'.";
				serviceResponse.Error(message);
				Log.LogCritical(message);
			}

			CanEdit(input.Id);

			if (!serviceResponse.Success)
				return serviceResponse;

			var allowedExtensions = new[] { ".gif", ".jpg", ".png", ".jpeg" };

			var extension = Path.GetExtension(input.NewAvatar.FileName).ToLower();

			if (!allowedExtensions.Contains(extension))
				serviceResponse.Error(nameof(input.NewAvatar), "Your avatar must end with .gif, .jpg, .jpeg, or .png");

			if (!serviceResponse.Success)
				return serviceResponse;

			using (var inputStream = input.NewAvatar.OpenReadStream()) {
				inputStream.Position = 0;

				userRecord.AvatarPath = await ImageStore.StoreImage(new ServiceModels.ImageStoreOptions {
					ContainerName = "avatars",
					FileName = $"avatar{userRecord.Id}",
					InputStream = inputStream,
					MaxDimension = SettingsRepository.AvatarSize(true),
					Overwrite = true
				});
			}

			DbContext.Update(userRecord);
			DbContext.SaveChanges();

			Log.LogInformation($"Avatar was modified by '{UserContext.ApplicationUser.DisplayName}' for account '{userRecord.DisplayName}'.");

			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> Register(InputModels.RegisterInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var birthday = new DateTime(input.BirthdayYear, input.BirthdayMonth, input.BirthdayDay);

			var user = new DataModels.ApplicationUser {
				DisplayName = input.DisplayName,
				Registered = DateTime.Now,
				LastOnline = DateTime.Now,
				UserName = input.Email,
				Email = input.Email,
				Birthday = birthday
			};

			var identityResult = await UserManager.CreateAsync(user, input.Password);

			if (identityResult.Succeeded) {
				Log.LogInformation($"User created a new account with password '{input.Email}'.");

				var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
				var callbackUrl = EmailConfirmationLink(user.Id, code);

				if (!EmailSender.Ready) {
					serviceResponse.RedirectPath = callbackUrl;
					return serviceResponse;
				}

				await EmailSender.SendEmailConfirmationAsync(input.Email, callbackUrl);
			}
			else {
				foreach (var error in identityResult.Errors) {
					Log.LogError($"Error registering '{input.Email}'. Message: {error.Description}");
					serviceResponse.Error(error.Description);
				}
			}

			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> SendVerificationEmail() {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var code = await UserManager.GenerateEmailConfirmationTokenAsync(UserContext.ApplicationUser);
			var callbackUrl = EmailConfirmationLink(UserContext.ApplicationUser.Id, code);
			var email = UserContext.ApplicationUser.Email;

			if (!EmailSender.Ready) {
				serviceResponse.RedirectPath = callbackUrl;
				return serviceResponse;
			}

			await EmailSender.SendEmailConfirmationAsync(email, callbackUrl);

			serviceResponse.Message = "Verification email sent. Please check your email.";
			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> ConfirmEmail(InputModels.ConfirmEmailInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var account = await UserManager.FindByIdAsync(input.UserId);

			if (account is null)
				serviceResponse.Error($"Unable to load account '{input.UserId}'.");

			if (serviceResponse.Success) {
				var identityResult = await UserManager.ConfirmEmailAsync(account, input.Code);

				if (!identityResult.Succeeded) {
					foreach (var error in identityResult.Errors) {
						Log.LogError($"Error confirming '{account.Email}'. Message: {error.Description}");
						serviceResponse.Error(error.Description);
					}
				}
				else
					Log.LogInformation($"User confirmed email '{account.Id}'.");
			}

			await SignOut();

			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> ForgotPassword(InputModels.ForgotPasswordInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var account = await UserManager.FindByNameAsync(input.Email);

			if (account != null && await UserManager.IsEmailConfirmedAsync(account)) {
				var code = await UserManager.GeneratePasswordResetTokenAsync(account);
				var callbackUrl = ResetPasswordCallbackLink(account.Id, code);

				if (!EmailSender.Ready) {
					serviceResponse.RedirectPath = callbackUrl;
					return serviceResponse;
				}

				await EmailSender.SendEmailAsync(input.Email, "Reset Password", $"Please reset your password by clicking here: <a href='{callbackUrl}'>link</a>");
			}

			serviceResponse.RedirectPath = UrlHelper.Action(nameof(Account.ForgotPasswordConfirmation));
			return serviceResponse;
		}

		public async Task<ServiceModels.ServiceResponse> ResetPassword(InputModels.ResetPasswordInput input) {
			var serviceResponse = new ServiceModels.ServiceResponse();

			var account = await UserManager.FindByEmailAsync(input.Email);

			if (account != null) {
				var identityResult = await UserManager.ResetPasswordAsync(account, input.Code, input.Password);

				if (!identityResult.Succeeded) {
					foreach (var error in identityResult.Errors)
						Log.LogError($"Error resetting password for '{account.Email}'. Message: {error.Description}");
				}
				else
					Log.LogInformation($"Password was reset for '{account.Email}'.");
			}

			serviceResponse.RedirectPath = nameof(Account.ResetPasswordConfirmation);
			return serviceResponse;
		}

		public async Task MergeAccounts(string sourceId, string targetId, bool eraseContent) {
			var sourceAccount = First(item => item.Id == sourceId);
			var targetAccount = First(item => item.Id == targetId);

			var siteSettings = DbContext.SiteSettings.Where(item => item.UserId == sourceId).ToList();
			DbContext.RemoveRange(siteSettings);

			var notifications = DbContext.Notifications.Where(item => item.UserId == sourceId).ToList();
			var participants = DbContext.Participants.Where(item => item.UserId == sourceId).ToList();
			var pins = DbContext.Pins.Where(item => item.UserId == sourceId).ToList();
			var viewLogs = DbContext.ViewLogs.Where(item => item.UserId == sourceId).ToList();

			if (eraseContent) {
				DbContext.RemoveRange(notifications);
				DbContext.RemoveRange(participants);
				DbContext.RemoveRange(pins);
				DbContext.RemoveRange(viewLogs);

				DbContext.SaveChanges();
			}
			else {
				foreach (var item in notifications) {
					item.UserId = targetId;
					DbContext.Update(item);
				}

				foreach (var item in participants) {
					item.UserId = targetId;
					DbContext.Update(item);
				}

				foreach (var item in pins) {
					item.UserId = targetId;
					DbContext.Update(item);
				}

				foreach (var item in viewLogs) {
					item.UserId = targetId;
					DbContext.Update(item);
				}
			}

			foreach (var item in DbContext.MessageBoards.Where(item => item.UserId == sourceId).ToList()) {
				item.UserId = targetId;
				DbContext.Update(item);
			}

			foreach (var item in DbContext.MessageThoughts.Where(item => item.UserId == sourceId).ToList()) {
				item.UserId = targetId;
				DbContext.Update(item);
			}

			foreach (var item in DbContext.Notifications.Where(item => item.TargetUserId == sourceId).ToList()) {
				item.TargetUserId = targetId;
				DbContext.Update(item);
			}

			DbContext.SaveChanges();

			foreach (var item in DbContext.Messages.Where(item => item.PostedById == sourceId).ToList()) {
				item.PostedById = targetId;
				item.EditedById = targetId;

				if (eraseContent) {
					item.OriginalBody = string.Empty;
					item.DisplayBody = "This account has been deleted.";
					item.LongPreview = string.Empty;
					item.ShortPreview = string.Empty;
					item.Cards = string.Empty;
				}
			}

			DbContext.SaveChanges();

			await UserManager.DeleteAsync(sourceAccount);

			DbContext.SaveChanges();
		}

		public void CanEdit(string userId) {
			if (userId == UserContext.ApplicationUser.Id || UserContext.IsAdmin)
				return;

			Log.LogWarning($"A user tried to edit another user's profile. {UserContext.ApplicationUser.DisplayName}");

			throw new HttpForbiddenError();
		}

		public async Task SignOut() {
			HttpContextAccessor.HttpContext.Session.Remove(Constants.Keys.UserId);

			await Task.WhenAll(new[] {
				HttpContextAccessor.HttpContext.Session.CommitAsync(),
				SignInManager.SignOutAsync()
			});
		}

		public string EmailConfirmationLink(string userId, string code) => UrlHelper.AbsoluteAction(nameof(Account.ConfirmEmail), nameof(Account), new { userId, code });
		public string ResetPasswordCallbackLink(string userId, string code) => UrlHelper.AbsoluteAction(nameof(Account.ResetPassword), nameof(Account), new { userId, code });
		protected override List<DataModels.ApplicationUser> GetRecords() => DbContext.Users.OrderBy(u => u.DisplayName).ToList();
	}
}