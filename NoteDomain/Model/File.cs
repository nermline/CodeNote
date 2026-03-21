using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoteDomain.Model;

public partial class File: Entity
{
    [Display(Name = "Ім\'я файлу")]
    [Required(ErrorMessage = "Ім\'я не повинно бути порожнім!")]
    public string Name { get; set; } = null!;

    [Display(Name = "Опис")]
    public string? Description { get; set; }

    [Display(Name = "Місце знаходження")]
    public int Folderid { get; set; }

    [Display(Name = "Дата створення")]
    public DateTime? Createdat { get; set; }

    public virtual ICollection<Fileversion> Fileversions { get; set; } = new List<Fileversion>();

    [Display(Name = "Місце знаходження")]
    public virtual Folder? Folder { get; set; } = null!;

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
