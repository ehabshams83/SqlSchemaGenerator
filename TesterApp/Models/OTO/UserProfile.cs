using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.OTO
{
    public class UserProfile
    {
        public int Id { get; set; } // PK = FK to User
        public string Bio { get; set; }
        public User User { get; set; }
    }

}
