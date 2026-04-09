using System;
using System.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PerformanceTrayMonitor.Extensions
{
	public static class ObservableCollectionExtensions
	{
		public static void ReplaceWith<T>(this ObservableCollection<T> col, IEnumerable<T> items)
		{
			var newList = items.ToList();

			// Fast path: same count and same items → do nothing
			if (col.Count == newList.Count && col.SequenceEqual(newList))
				return;

			col.Clear();
			foreach (var item in newList)
				col.Add(item);
		}
	}
}
