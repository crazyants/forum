﻿using Forum3.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Forum3.Extensions {
	public static class UrlHelperExtensions {
		public static string AbsoluteAction(this IUrlHelper url, string actionName, string controllerName, object routeValues = null) {
			var scheme = url.ActionContext.HttpContext.Request.Scheme;
			return url.Action(actionName, controllerName, routeValues, scheme);
		}

		public static string DirectMessage(this IUrlHelper url, int messageId) => url.Action(nameof(Topics.Display), nameof(Topics), new { id = messageId }) + $"#message{messageId}";
	}
}