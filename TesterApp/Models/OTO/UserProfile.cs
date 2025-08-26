using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.OTO
{
    public class UserProfile
    {
        public int Id { get; set; }
        public string Bio { get; set; }
        public int UserId { get; set; } // ⬅️ ضروري
        public User User { get; set; }
    }



}
