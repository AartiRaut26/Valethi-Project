/*using Microsoft.AspNetCore.Http;
using Sieve.Models;
using System;
using System.Collections.Generic;
using System.Linq;
namespace NewStudentAttendenceAPI.Extensions
{
	public static class HttpRequestExtensions
	{
		public static SieveModel ToSieveModel(this HttpRequest request)
		{
			var sieveModel = new SieveModel();

			if (request.Query.ContainsKey("filters"))
			{
				var filters = request.Query["filters"].ToString();
				if (!string.IsNullOrEmpty(filters))
				{
					sieveModel.Filters = ParseFilters(filters);
				}
			}

			if (request.Query.ContainsKey("sorts"))
			{
				var sorts = request.Query["sorts"].ToString();
				if (!string.IsNullOrEmpty(sorts))
				{
					sieveModel.Sorts = ParseSorts(sorts);
				}
			}

			return sieveModel;
		}

		private static string ParseFilters(string filterString)
		{
			// You need to convert the List<SieveFilter> to a string
			// Here, I'm joining the filters with a semicolon separator
			var filterList = filterString.Split(';')
				.Select(filter => new SieveFilter
				{
					Field = filter.Split(':')[0],
					Operator = filter.Split(':')[1],
					Value = filter.Split(':')[2]
				})
				.ToList();

			return string.Join(";", filterList.Select(f =>
				$"{f.Field}:{f.Operator}:{f.Value}"));
		}

		private static string ParseSorts(string sortString)
		{
			// You need to convert the List<SieveSort> to a string
			// Here, I'm joining the sorts with a comma separator
			var sortList = sortString.Split(',')
				.Select(sort => new SieveSort
				{
					Field = sort.Split(':')[0],
					Order = sort.Split(':')[1]
				})
				.ToList();

			return string.Join(",", sortList.Select(s =>
				$"{s.Field}:{s.Order}"));
		}
	}
}
*/