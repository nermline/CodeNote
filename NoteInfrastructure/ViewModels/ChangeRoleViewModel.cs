using Microsoft.AspNetCore.Identity;

namespace NoteInfrastructure.ViewModels;

public class ChangeRoleViewModel
{
    public string UserId { get; set; } = null!;
    public string UserEmail { get; set; } = null!;
    public List<IdentityRole> AllRoles { get; set; } = [];
    public IList<string> UserRoles { get; set; } = [];
}
