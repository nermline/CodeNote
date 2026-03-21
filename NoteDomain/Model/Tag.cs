using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoteDomain.Model;

public partial class Tag: Entity
{
    [Display(Name = "Назва")]
    [Required(ErrorMessage = "Назва не може бути порожньою!")]
    public string Name { get; set; } = null!;

    public virtual ICollection<File> Files { get; set; } = new List<File>();
}
