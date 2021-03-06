﻿using Forum3.Models.ViewModels.Topics.Items;
using System.Collections.Generic;

namespace Forum3.Models.ViewModels.Topics.Pages {
	public class TopicIndexPage {
		/// <summary>
		/// Used in topic merging
		/// </summary>
		public int SourceId { get; set; }

		public int BoardId { get; set; }
		public int Page { get; set; }
		public int UnreadFilter { get; set; }
		public string BoardName { get; set; }
		public List<MessagePreview> Topics { get; set; }

		public Sidebar Sidebar { get; set; }
	}
}