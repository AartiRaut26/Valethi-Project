using System.Collections.Generic;

namespace NewStudentAttendenceAPI.DTOs
{
	public class JsonPatchDocumentDto
	{
		public List<OperationDto> Operations { get; set; }

	}
}
