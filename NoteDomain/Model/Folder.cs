using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoteDomain.Model;

public partial class Folder: Entity
{
    [Display(Name="Ім\'я Каталогу")]
    [Required(ErrorMessage="Ім\'я каталогу не повинно бути порожнім!")]
    public string Name { get; set; } = null!;

    [Display(Name = "Батьківський каталог")]
    public int? Parentfolderid { get; set; }

    [Display(Name = "Дата створення")]
    public DateTime? Createdat { get; set; }

    public virtual ICollection<File> Files { get; set; } = new List<File>();

    public virtual ICollection<Folder> InverseParentfolder { get; set; } = new List<Folder>();

    [Display(Name = "Батьківський каталог")]
    public virtual Folder? Parentfolder { get; set; }
}
