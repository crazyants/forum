﻿using System;
using System.Collections.Generic;

namespace Forum3.Interfaces.Models {
	public interface IRepository<T> : IEnumerable<T> where T : class {
		T this[int i] { get; }
		T First(Func<T, bool> predicate);
	}
}