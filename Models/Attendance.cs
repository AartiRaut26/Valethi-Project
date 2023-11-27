using NewStudentAttendenceAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewStudentAttendenceAPI.Models
{
	public class Attendance
	{
		
		public int AttendanceId { get; set; }
		public int StudentId { get; set; }
		public Student Student { get; set; }
		public int ClassId { get; set; }
		public Class Class { get; set; }
		public DateTime Time { get; set; }
	}
}
