﻿using Forum3.Interfaces.Models.ViewModels;

namespace Forum3.Models.ViewModels.Topics.Items {
	public class ReplyForm : IMessageViewModel {
		public int Id { get; set; }
		public int? BoardId { get; set; }
		public string Body { get; set; }
		public string FormAction { get; } = nameof(Controllers.Topics.TopicReply);
		public string FormController { get; } = nameof(Controllers.Topics);
		public string CancelPath { get; set; }
	}
}