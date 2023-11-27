using System.ComponentModel.DataAnnotations.Schema;

namespace NewStudentAttendenceAPI.Models
{
	public class Student
	{
		public int StudentId { get; set; }
		public string StudentName { get; set; }
		public ICollection<NewStudentAttendenceAPI.Models.Attendance> Attendances { get; set; }
	}
}
