using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewStudentAttendenceAPI;
using NewStudentAttendenceAPI.Configuration;
using NewStudentAttendenceAPI.DTOs;
using NewStudentAttendenceAPI.Models;
using NewStudentAttendenceAPI.Services;
using StackExchange.Redis;
//using StudentAttendance.Services;
using System.Text.Json;
using System.Text.Json.Serialization;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//redis db for in memory data storage
var redisConfiguration = builder.Configuration.GetSection("RedisConfiguration").Get<RedisConfiguration>();
var redis = ConnectionMultiplexer.Connect($"{redisConfiguration.Host}:{redisConfiguration.Port}");

// Register IDistributedCache service
builder.Services.AddStackExchangeRedisCache(options =>
{
	options.Configuration = $"{redisConfiguration.Host}:{redisConfiguration.Port}";
});

// Register the RedisService and IConnectionMultiplexer with the DI container
builder.Services.AddSingleton<RedisService>();
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);



//Dependancy Injection
builder.Services.AddDbContext<StudentAttDBContext>(opt => opt.UseSqlServer(builder.Configuration.GetConnectionString("DBConnection")));


builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddControllers()
	.AddNewtonsoftJson();


var app = builder.Build();

var options = new JsonSerializerOptions
{
	ReferenceHandler = ReferenceHandler.Preserve,
};

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();



app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));

app.MapGet("/", (HttpContext context) =>
{
	context.Response.Redirect("https://localhost:7169/swagger/index.html");
	return context.Response.CompleteAsync();
});


var studs = app.MapGroup("/api/students");
var classes = app.MapGroup("/api/classes");
var Attendances = app.MapGroup("/api/Attendances");

studs.MapGet("/", GetAllStudents);
studs.MapGet("/{id}", GetStudentById);
studs.MapPost("/", CreateStudent);
studs.MapPatch("/{id}", PatchStudent);
studs.MapPut("/{id}", UpdateStudent);
studs.MapDelete("/{id}", DeleteStudent);

classes.MapGet("/", GetAllClasses);
classes.MapGet("/{id}", GetClassById);
classes.MapPost("/", CreateClasses); 
classes.MapPatch("/{id}", PatchClass);
classes.MapPut("/{id}", UpdateClass);
classes.MapDelete("/{id}", DeleteClass);

Attendances.MapGet("/", GetAllAttendances);
Attendances.MapGet("/{id}", GetAttendanceById);
Attendances.MapPost("/",CreateAttendances);
Attendances.MapPatch("/{id}", PatchAttendance);
Attendances.MapPut("/{studentId}/{classId}", UpdateAttendances);
Attendances.MapDelete("/{id}", DeleteAttendances);


/*  ---------------------------------------------------------------------------- */
static async Task<IResult> GetAllStudents( StudentAttDBContext context, RedisService redisService)
{
	var students = await redisService.GetStudentsWithCachingAsync(context);
	return Results.Ok(students);
}
/*------------------------------------------------------------------------------------- */

static async Task<IResult> GetStudentById(int studentId,  StudentAttDBContext context, RedisService redisService)
{
	var student = await redisService.GetStudentByIdWithCachingAsync(studentId, context);

	if (student != null)
	{
		return TypedResults.Ok(student);
	}
	else
	{
		return Results.NotFound("Student not found.");
	}
}

/*  ---------------------------------------------------------------------------- */

static async Task<IResult> GetAllClasses(StudentAttDBContext context, RedisService redisService)
{
	var classes = await redisService.GetCachedDataAsync<List<Class>>("classes");

	if (classes == null)
	{
		classes = await context.Classes.ToListAsync();

		if (classes.Count > 0)
		{
			await redisService.CacheDataAsync("classes", classes);
		}
	}

	return Results.Ok(classes);
}

static async Task<IResult> GetClassById(int classId, StudentAttDBContext context, RedisService redisService)
{
	var classData = await redisService.GetCachedDataAsync<Class>($"class:{classId}");

	if (classData == null)
	{
		classData = await context.Classes.FirstOrDefaultAsync(c => c.ClassId == classId);

		if (classData != null)
		{
			await redisService.CacheDataAsync($"class:{classId}", classData);
			return TypedResults.Ok(classData);
		}
		else
		{
			return Results.NotFound("Class not found.");
		}
	}

	return TypedResults.Ok(classData);
}

/*  ---------------------------------------------------------------------------- */

static async Task<IResult> GetAllAttendances(StudentAttDBContext context, RedisService redisService)
{
	var cachedAttendances = await redisService.GetCachedDataAsync<List<Attendance>>("attendances");

	if (cachedAttendances != null)
	{
		return Results.Ok(cachedAttendances);
	}

	var attendances = await context.Attendances.ToListAsync();

	// Cache the data
	if (attendances.Count > 0)
	{
		await redisService.CacheDataAsync("attendances", attendances);
	}

	return Results.Ok(attendances);
}

/*  ---------------------------------------------------------------------------- */
static async Task<IResult> GetAttendanceById(int attendanceId, StudentAttDBContext context, RedisService redisService)
{
	var cachedAttendance = await redisService.GetCachedDataAsync<Attendance>($"attendance:{attendanceId}");

	if (cachedAttendance != null)
	{
		return TypedResults.Ok(cachedAttendance);
	}

	try
	{
		var attendance = await context.Attendances.FirstOrDefaultAsync(a => a.AttendanceId == attendanceId);

		if (attendance != null)
		{
			await redisService.CacheDataAsync($"attendance:{attendanceId}", attendance);
			return TypedResults.Ok(attendance);
		}
		else
		{
			return Results.NotFound("Attendance not found.");
		}
	}
	catch (Exception ex)
	{
		return Results.BadRequest($"Failed to retrieve attendance. Error: {ex.Message}");
	}
}
/*  ---------------------------------------------------------------------------- */
static async Task<IResult> CreateStudent(Student student, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();
	try
	{
		// Check if the student already exists by name
		var existingStudent = await context.Students.FirstOrDefaultAsync(s => s.StudentName == student.StudentName);
		if (existingStudent == null)
		{
			// Student doesn't exist, so add them to the database
			context.Students.Add(student);
		}
		else
		{
			// Student exists, attach it to the context
			context.Students.Attach(existingStudent);
			student.StudentId = existingStudent.StudentId; // Ensure the StudentId is set
		}

		await context.SaveChangesAsync();


        

        // Cache the created or updated student
        await redisService.CacheStudentAsync1(student);
		await transaction.CommitAsync();
		// Return the created or existing student
		return TypedResults.Created($"/students/{student.StudentId}", student);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to create student. Error: {ex.Message}");
	}
}
/******************************************************/

/*  ---------------------------------------------------------------------------- */

static async Task<IResult> CreateClasses(Class classModel, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the class already exists by name
		var existingClass = await context.Classes.FirstOrDefaultAsync(c => c.ClassName == classModel.ClassName);

		if (existingClass == null)
		{
			// Class doesn't exist, so add it to the database
			context.Classes.Add(classModel);
		}
		else
		{
			// Class exists, attach it to the context
			context.Classes.Attach(existingClass);
			classModel.ClassId = existingClass.ClassId; // Ensure the ClassId is set
		}

		// Clear the corresponding data from the cache
		await redisService.ClearClassesFromCacheAsync();

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		return TypedResults.Created($"/classes/{classModel.ClassId}", classModel);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to create class. Error: {ex.Message}");
	}
}
/*  ---------------------------------------------------------------------------- */
static async Task<IResult> CreateAttendances(Attendance attendance, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the attendance already exists based on some criteria (e.g., time, student, class)
		var existingAttendance = await context.Attendances.FirstOrDefaultAsync(a =>
			a.Time == attendance.Time && a.StudentId == attendance.StudentId && a.ClassId == attendance.ClassId);

		if (existingAttendance == null)
		{
			// Attendance doesn't exist, so add it to the database
			context.Attendances.Add(attendance);
		}
		else
		{
			// Attendance exists, attach it to the context
			context.Attendances.Attach(existingAttendance);
			// Ensure the AttendanceId is set
			attendance.AttendanceId = existingAttendance.AttendanceId;
		}

		// Clear the corresponding data from the cache
		await redisService.ClearAttendancesFromCacheAsync();

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		return TypedResults.Created($"/attendances/{attendance.AttendanceId}", attendance);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to create attendance. Error: {ex.Message}");
	}
}

/*  ---------------------------------------------------------------------------- */

static async Task<IResult> UpdateStudent(HttpContext httpContext, Student student, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the student to be updated exists by ID
		var existingStudent = await context.Students.FindAsync(student.StudentId);

		if (existingStudent == null)
		{
			// Student doesn't exist, return an error
			return Results.NotFound("Student not found.");
		}

		// Update the existing student's information
		existingStudent.StudentName = student.StudentName;
		// Update other properties as needed

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		// Clear the cached students' data
		await redisService.ClearStudentsFromCacheAsync();

		// Return the updated student
		return TypedResults.Ok(existingStudent);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to update student. Error: {ex.Message}");
	}
}


/*  ---------------------------------------------------------------------------- */

static async Task<IResult> PatchStudent(int id, JsonPatchDocumentDto patchDoc, [FromServices] StudentAttDBContext context, [FromServices] RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		var student = context.Students.Find(id);

		if (student == null)
		{
			return Results.NotFound("Student not found.");
		}

		foreach (var operation in patchDoc.Operations)
		{
			if (operation.Op == "replace")
			{
				var property = operation.Path.TrimStart('/'); // Remove the leading '/'
				var value = operation.Value;

				// Find the corresponding property on the 'student' object
				var studentProperty = typeof(Student).GetProperty(property);

				if (studentProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, studentProperty.PropertyType);

						// Apply the patch operation
						studentProperty.SetValue(student, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						await transaction.RollbackAsync();
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						await transaction.RollbackAsync();
						return Results.BadRequest($"Failed to apply the patch operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					await transaction.RollbackAsync();
					return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
				}
			}
			else if (operation.Op == "add")
			{
				var property = operation.Path.TrimStart('/');
				var value = operation.Value;

				// Find the corresponding property on the 'student' object
				var studentProperty = typeof(Student).GetProperty(property);

				if (studentProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, studentProperty.PropertyType);

						// Check if the property value is null
						if (typedValue == null)
						{
							await transaction.RollbackAsync();
							return Results.BadRequest("Value cannot be null for the 'add' operation.");
						}

						// Apply the 'add' operation
						studentProperty.SetValue(student, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						await transaction.RollbackAsync();
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						await transaction.RollbackAsync();
						return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					await transaction.RollbackAsync();
					return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
				}
			}
			else if (operation.Op == "remove")
			{
				// Handle 'remove' operation
				var property = operation.Path.TrimStart('/');
				var studentProperty = typeof(Student).GetProperty(property);

				if (studentProperty != null)
				{
					// Set the property value to null to remove it
					studentProperty.SetValue(student, null);
				}
				else
				{
					// Handle the case where the property doesn't exist
					await transaction.RollbackAsync();
					return Results.NotFound($"Property '{property}' not found on the 'Student' object.");
				}
			}

			await redisService.ClearStudentFromCacheAsync(id);
		}

		// Save changes to the database
		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		return Results.NoContent();
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to update student. Error: {ex.Message}");
	}
}

/*  ---------------------------------------------------------------------------- */
static async Task<IResult> DeleteStudent(HttpContext httpContext, int studentId, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the student to be deleted exists by ID
		var existingStudent = context.Students.Find(studentId);

		if (existingStudent == null)
		{
			// Student doesn't exist, return a not found error
			await transaction.RollbackAsync();
			return Results.NotFound("Student not found.");
		}

		// Remove the student from the database
		context.Students.Remove(existingStudent);

		// Clear the cached student data
		await redisService.ClearStudentFromCacheAsync(studentId);

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		// Return a success message or status
		return Results.Ok("Student deleted successfully.");
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to delete student. Error: {ex.Message}");
	}
}
/*  ---------------------------------------------------------------------------- */
static async Task<IResult> UpdateClass(Class classModel, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the class to be updated exists by ID
		var existingClass = context.Classes.Find(classModel.ClassId);

		if (existingClass == null)
		{
			// Class doesn't exist, return a not found error
			await transaction.RollbackAsync();
			await transaction.RollbackAsync();
			return Results.NotFound("Class not found.");
		}

		// Update the existing class's information
		existingClass.ClassName = classModel.ClassName;
		// Update other properties as needed

		// Clear the cached class data
		await redisService.ClearClassFromCacheAsync(classModel.ClassId);

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		// Return the updated class
		return TypedResults.Ok(existingClass);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to update class. Error: {ex.Message}");
	}
}
/*  ---------------------------------------------------------------------------- */
static async Task<IResult> PatchClass(int id, JsonPatchDocumentDto patchDoc, [FromServices] StudentAttDBContext context, RedisService redisService)
{
	try
	{
		// Check if the class to be updated exists by ID
		var existingClass = await context.Classes.FindAsync(id);

		if (existingClass == null)
		{
			// Class doesn't exist, return a not found error
			return Results.NotFound("Class not found.");
		}

		foreach (var operation in patchDoc.Operations)
		{
			if (operation.Op == "replace")
			{
				var property = operation.Path.TrimStart('/'); // Remove the leading '/'
				var value = operation.Value;

				// Find the corresponding property on the 'existingClass' object
				var classProperty = typeof(Class).GetProperty(property);

				if (classProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, classProperty.PropertyType);

						// Apply the 'replace' patch operation
						classProperty.SetValue(existingClass, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						return Results.BadRequest($"Failed to apply the 'replace' operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
				}
			}
			else if (operation.Op == "add")
			{
				var property = operation.Path.TrimStart('/');
				var value = operation.Value;

				// Find the corresponding property on the 'existingClass' object
				var classProperty = typeof(Class).GetProperty(property);

				if (classProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, classProperty.PropertyType);

						// Check if the property value is null
						if (typedValue == null)
						{
							return Results.BadRequest("Value cannot be null for the 'add' operation.");
						}

						// Apply the 'add' operation
						classProperty.SetValue(existingClass, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
				}
			}
			else if (operation.Op == "remove")
			{
				var property = operation.Path.TrimStart('/');
				var classProperty = typeof(Class).GetProperty(property);

				if (classProperty != null)
				{
					// Set the property value to null to remove it
					classProperty.SetValue(existingClass, null);
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Class' object.");
				}
			}
			// You can add more handling for other operations here
		}

		await redisService.ClearClassFromCacheAsync(id);

		// Save changes to the database
		await context.SaveChangesAsync();

		// Return the updated class
		return TypedResults.Ok(existingClass);
	}
	catch (Exception ex)
	{
		return Results.BadRequest($"Failed to update class. Error: {ex.Message}");
	}
}

/*  ---------------------------------------------------------------------------- */
// Minimal API endpoint
static async Task<IResult> DeleteClass(int classId, StudentAttDBContext context, RedisService redisService)
{
    using var transaction = await context.Database.BeginTransactionAsync();

    try
    {
        var existingClass = await context.Classes.FindAsync(classId);

        if (existingClass == null)
        {
            await transaction.RollbackAsync();
            return Results.NotFound("Class not found.");
        }

        context.Classes.Remove(existingClass);

        await redisService.ClearClassFromCacheAsync(classId);

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Results.Ok("Class deleted successfully.");
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return Results.BadRequest($"Failed to delete class. Error: {ex.Message}");
    }
}

/*  ---------------------------------------------------------------------------- */
static async Task<IResult> UpdateAttendances([FromRoute] int studentId, [FromRoute] int classId, [FromBody] Attendance updatedAttendance, [FromServices] StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Find the existing attendance by the composite key
		var existingAttendance = context.Attendances.Find(studentId, classId);

		if (existingAttendance == null)
		{
			await transaction.RollbackAsync();
			// Attendance doesn't exist, return a not found error
			return Results.NotFound("Attendance not found.");
		}

		// Update the properties you need to change
		existingAttendance.Time = updatedAttendance.Time; // Update with actual property names

		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		// Remove the cached attendance data
		await redisService.ClearAttendanceFromCacheAsync(studentId, classId);

		// Return the updated attendance
		return TypedResults.Ok(existingAttendance);
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to update attendance. Error: {ex.Message}");
	}
}
/* ----------------------------------------------------------------------------  */

static async Task<IResult> PatchAttendance(int studentId,  int classId, JsonPatchDocumentDto patchDoc, [FromServices] StudentAttDBContext context, RedisService redisService)
{
	try
	{
		// Check if the attendance to be updated exists by the composite key
		var existingAttendance = context.Attendances.Find(studentId, classId);

		if (existingAttendance == null)
		{
			// Attendance doesn't exist, return a not found error
			return Results.NotFound("Attendance not found.");
		}

		foreach (var operation in patchDoc.Operations)
		{
			if (operation.Op == "replace")
			{
				var property = operation.Path.TrimStart('/'); // Remove the leading '/'
				var value = operation.Value;

				// Find the corresponding property on the 'existingAttendance' object
				var attendanceProperty = typeof(Attendance).GetProperty(property);

				if (attendanceProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, attendanceProperty.PropertyType);

						// Apply the 'replace' patch operation
						attendanceProperty.SetValue(existingAttendance, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						return Results.BadRequest($"Failed to apply the 'replace' operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
				}
			}
			else if (operation.Op == "add")
			{
				var property = operation.Path.TrimStart('/');
				var value = operation.Value;

				// Find the corresponding property on the 'existingAttendance' object
				var attendanceProperty = typeof(Attendance).GetProperty(property);

				if (attendanceProperty != null)
				{
					try
					{
						// Convert the 'value' to the correct data type for the property
						var typedValue = Convert.ChangeType(value, attendanceProperty.PropertyType);

						// Check if the property value is null
						if (typedValue == null)
						{
							return Results.BadRequest("Value cannot be null for the 'add' operation.");
						}

						// Apply the 'add' operation
						attendanceProperty.SetValue(existingAttendance, typedValue);
					}
					catch (InvalidCastException)
					{
						// Handle invalid data type conversion
						return Results.BadRequest("Invalid data type for the property.");
					}
					catch (Exception ex)
					{
						// Handle other exceptions that may occur
						return Results.BadRequest($"Failed to apply the 'add' operation. Error: {ex.Message}");
					}
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
				}
			}
			else if (operation.Op == "remove")
			{
				var property = operation.Path.TrimStart('/');
				var attendanceProperty = typeof(Attendance).GetProperty(property);

				if (attendanceProperty != null)
				{
					// Set the property value to null to remove it
					attendanceProperty.SetValue(existingAttendance, null);
				}
				else
				{
					// Handle the case where the property doesn't exist
					return Results.NotFound($"Property '{property}' not found on the 'Attendance' object.");
				}
			}
			// You can add more handling for other operations here
		}

		// Save changes to the database
		await context.SaveChangesAsync();

		// Remove the cached attendance data
		await redisService.ClearAttendanceFromCacheAsync(studentId, classId);

		// Return the updated attendance
		return TypedResults.Ok(existingAttendance);
	}
	catch (Exception ex)
	{
		return Results.BadRequest($"Failed to update attendance. Error: {ex.Message}");
	}
}
/*  ---------------------------------------------------------------------------- */

static async Task<IResult> DeleteAttendances(int attendanceId, StudentAttDBContext context, RedisService redisService)
{
	using var transaction = await context.Database.BeginTransactionAsync();

	try
	{
		// Check if the attendance to be deleted exists based on the specified criteria (e.g., time, student, class)
		var existingAttendance = context.Attendances.FirstOrDefault(a => a.AttendanceId == attendanceId);

		if (existingAttendance == null)
		{
			await transaction.RollbackAsync();
			// Attendance doesn't exist, return a not found error
			return Results.NotFound("Attendance not found.");
		}

		// Remove the attendance from the database
		context.Attendances.Remove(existingAttendance);
		await context.SaveChangesAsync();
		await transaction.CommitAsync();

		// Remove the cached attendance data
		await redisService.ClearAttendanceFromCacheAsync(existingAttendance.StudentId, existingAttendance.ClassId);

		// Return a success message or status
		return Results.Ok("Attendance deleted successfully.");
	}
	catch (Exception ex)
	{
		await transaction.RollbackAsync();
		return Results.BadRequest($"Failed to delete attendance. Error: {ex.Message}");
	}
}
app.Run();
