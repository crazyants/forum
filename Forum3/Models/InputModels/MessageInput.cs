﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Forum3.Models.InputModels {
	public class MessageInput {
		public int Id { get; set; }

		[Required]
		public string Body { get; set; }

		public int? BoardId { get; set; }

		public List<int> SelectedBoards { get; set; } = new List<int>();
	}
}