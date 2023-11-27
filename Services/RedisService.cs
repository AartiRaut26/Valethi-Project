using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using NewStudentAttendenceAPI.Models;
using NewStudentAttendenceAPI.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace NewStudentAttendenceAPI.Services
{

	//Service class to store the caching data into redis 
	public class RedisService
	{
		private readonly IConnectionMultiplexer _redis;
		private readonly IDistributedCache _cache;

		public RedisService(IConnectionMultiplexer redis, IDistributedCache cache)
		{
			_redis = redis;
			_cache = cache;
		}

		

		public async Task<List<Student>> GetStudentsWithCachingAsync(StudentAttDBContext context)
		{
			var cachedStudents = await GetStudentsFromCacheAsync();
			if (cachedStudents != null)
			{
				return cachedStudents;
			}

			var students = await context.Students.ToListAsync();

			// Cache the data
			if (students.Count > 0)
			{
                await CacheStudentsAsync(students);
			}

			return students;
		}

		public async Task<Student> GetStudentByIdWithCachingAsync(int studentId,  StudentAttDBContext context)
		{
			var cachedStudent = await GetStudentByIdFromCacheAsync(studentId);
			if (cachedStudent != null)
			{
				return cachedStudent;
			}

			var student = await context.Students.FirstOrDefaultAsync(s => s.StudentId == studentId);

			if (student != null)
			{
				await CacheStudentAsync(student);
				return student;
			}
			else
			{
				return null;
			}
		}
		public async Task ClearStudentFromCacheAsync(int studentId)
		{
			await _cache.RemoveAsync($"student:{studentId}");
		}


		public async Task<List<Student>> GetStudentsFromCacheAsync()
		{
			var cachedStudents = await _cache.GetStringAsync("students");

			if (!string.IsNullOrEmpty(cachedStudents))
			{
				return JsonSerializer.Deserialize<List<Student>>(cachedStudents);
			}

			return null;
		}

		public async Task CacheStudentsAsync(List<Student> students)
		{
			if (students.Count > 0)
			{
				var cacheOptions = new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
				};

				var serializedStudents = JsonSerializer.Serialize(students);
				await _cache.SetStringAsync("students", serializedStudents, cacheOptions);
			}
		}

		public async Task CacheStudentAsync1(Student student)
		{
			var cacheOptions = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
			};

			var serializedStudent = JsonSerializer.Serialize(student);
			await _cache.SetStringAsync($"student:{student.StudentId}", serializedStudent, cacheOptions);
		}
		public async Task ClearStudentsFromCacheAsync()
		{
			await _cache.RemoveAsync("students");
		}

		public async Task<Student> GetStudentByIdFromCacheAsync(int studentId)
		{
			var cachedStudent = await _cache.GetStringAsync($"student:{studentId}");

			if (!string.IsNullOrEmpty(cachedStudent))
			{
				return JsonSerializer.Deserialize<Student>(cachedStudent);
			}

			return null;
		}

		public async Task CacheStudentAsync(Student student)
		{
			var cacheOptions = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
			};

			var serializedStudent = JsonSerializer.Serialize(student);
			await _cache.SetStringAsync($"student:{student.StudentId}", serializedStudent, cacheOptions);
		}

		public async Task<T> GetCachedDataAsync<T>(string key)
		{
			var cachedData = await _cache.GetStringAsync(key);

			if (!string.IsNullOrEmpty(cachedData))
			{
				return JsonSerializer.Deserialize<T>(cachedData);
			}

			return default(T);
		}

		public async Task CacheDataAsync(string key, object data)
		{
			if (data != null)
			{
				var cacheOptions = new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
				};

				var serializedData = JsonSerializer.Serialize(data);
				await _cache.SetStringAsync(key, serializedData, cacheOptions);
			}
		}

		//create classes
		public async Task ClearClassesFromCacheAsync()
		{
			await _cache.RemoveAsync("classes");
		}
		public async Task ClearClassFromCacheAsync(int classId)
		{
			await _cache.RemoveAsync($"class:{classId}");
		}


		public async Task<List<Class>> GetClassesWithCachingAsync(StudentAttDBContext context)
		{
			var cachedClasses = await GetClassesFromCacheAsync();
			if (cachedClasses != null)
			{
				return cachedClasses;
			}

			var classes = await context.Classes.ToListAsync();

			// Cache the data
			if (classes.Count > 0)
			{
				await CacheClassesAsync(classes);
			}

			return classes;
		}
		public async Task<List<Class>> GetClassesFromCacheAsync()
		{
			var cachedClasses = await _cache.GetStringAsync("classes");

			if (!string.IsNullOrEmpty(cachedClasses))
			{
				return JsonSerializer.Deserialize<List<Class>>(cachedClasses);
			}

			return null;
		}

		public async Task CacheClassesAsync(List<Class> classes)
		{
			if (classes.Count > 0)
			{
				var cacheOptions = new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
				};

				var serializedClasses = JsonSerializer.Serialize(classes);
				await _cache.SetStringAsync("classes", serializedClasses, cacheOptions);
			}
		}

		//for attendance
		public async Task ClearAttendancesFromCacheAsync()
		{
			await _cache.RemoveAsync("attendances");
		}
		public async Task ClearAttendanceFromCacheAsync(int studentId, int classId)
		{
			await _cache.RemoveAsync($"attendance:{studentId}:{classId}");
		}

		public async Task<List<Attendance>> GetAttendancesWithCachingAsync(StudentAttDBContext context)
		{
			var cachedAttendances = await GetAttendancesFromCacheAsync();
			if (cachedAttendances != null)
			{
				return cachedAttendances;
			}

			var attendances = await context.Attendances.ToListAsync();

			// Cache the data
			if (attendances.Count > 0)
			{
				await CacheAttendancesAsync(attendances);
			}

			return attendances;
		}

		public async Task<List<Attendance>> GetAttendancesFromCacheAsync()
		{
			var cachedAttendances = await _cache.GetStringAsync("attendances");

			if (!string.IsNullOrEmpty(cachedAttendances))
			{
				return JsonSerializer.Deserialize<List<Attendance>>(cachedAttendances);
			}

			return null;
		}

		public async Task CacheAttendancesAsync(List<Attendance> attendances)
		{
			if (attendances.Count > 0)
			{
				var cacheOptions = new DistributedCacheEntryOptions
				{
					AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
				};

				var serializedAttendances = JsonSerializer.Serialize(attendances);
				await _cache.SetStringAsync("attendances", serializedAttendances, cacheOptions);
			}
		}

	}
}
