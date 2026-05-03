using Microsoft.AspNetCore.Identity;

namespace NoteInfrastructure.Models;

public class AppUser : IdentityUser
{

    public int? Year { get; set; }
}
