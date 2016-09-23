﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Forum3.Services;

namespace Forum3.Controllers {
	[Authorize]
	public class TopicsController : Controller {
		public TopicRepository _topics { get; set; }
		public MessageRepository _messages { get; set; }

		public TopicsController(TopicRepository topicRepo, MessageRepository messageRepo) {
			_topics = topicRepo;
			_messages = messageRepo;
		}

		[AllowAnonymous]
		public async Task<IActionResult> Index() {
			var skip = 0;

			if (HttpContext.Request.Query.ContainsKey("skip"))
				skip = Convert.ToInt32(HttpContext.Request.Query["skip"]);

			var take = 15;

			if (HttpContext.Request.Query.ContainsKey("take"))
				take = Convert.ToInt32(HttpContext.Request.Query["take"]);

			var viewModel = await _topics.GetTopicIndexAsync(skip, take);

			return View(viewModel);
		}

		public async Task<IActionResult> Display(int id, int page = 1) {
			var jumpToLatest = false;

			if (page == 0) {
				page = 1;
				jumpToLatest = true;
			}

			int take = 15;
			int skip = (page * take) - take;

			var message = _messages.Find(id);

			if (message.ParentId > 0) {
				var actionUrl = Url.Action("Display", "Topics", new { id = message.ParentId });
				return new RedirectResult(actionUrl + "#message" + id);
			}

			var viewModel = await _topics.GetTopicAsync(message, page, skip, take, jumpToLatest);

			return View(viewModel);
		}
	}
}