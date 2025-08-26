using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.MTMT
{
    public class Student
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public ICollection<Enrollment> Enrollments { get; set; }
    }
}
