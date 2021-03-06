﻿using Forum3.Annotations;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Forum3.Models.InputModels {
	public class CreateSmileyInput {
		[Required]
		[MinLength(2)]
		[MaxLength(10)]
		public string Code { get; set; }

		[MaxLength(200)]
		public string Thought { get; set; }

		[Required]
		[MaxFileSize(1024, ErrorMessage = "Maximum allowed file size is {0} KB")]
		public IFormFile File { get; set; }
	}
}