using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NoteDomain.Model;

public partial class Fileversion: Entity
{
    [Display(Name = "Цільовий файл")]
    [Required(ErrorMessage = "Не повинно бути порожнім!")]
    public int Fileid { get; set; }

    [Display(Name = "Вміст")]
    public string? Content { get; set; }

    [Display(Name = "Номер версії")]
    [Required(ErrorMessage = "Номер версії не повинен бути порожнім!")]
    public int Versionnumber { get; set; }

    [Display(Name = "Зміни")]
    public string? Changelog { get; set; }

    [Display(Name = "Створено")]
    public DateTime? Createdat { get; set; }

    [Display(Name = "Цільовий файл")]
    public virtual File? File { get; set; } = null!;
}
