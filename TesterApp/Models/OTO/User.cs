using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.OTO
{
    [Description("Represents a user in the system.")]
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [Range(18, 99)]
        public int Age { get; set; }

        [Description("Full name of the user.")]
        public string FullName => $"{FirstName} {LastName}";

        public string FirstName { get; set; }
        public string LastName { get; set; }

        public DateTime CreatedAt { get; set; }
        public UserProfile Profile { get; set; }
    }


}
