﻿using System;

namespace Forum3.Models.DataModels {
	public class Participant {
		public int Id { get; set; }
		public string UserId { get; set; }
		public int MessageId { get; set; }
		public DateTime Time { get; set; }
	}
}