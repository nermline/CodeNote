using NoteDomain.Model;
using NoteInfrastructure.Helpers;

namespace NoteInfrastructure.Models
{
    public class SearchResultViewModel
    {
        public string Query { get; set; } = "";

        public PaginatedList<NoteDomain.Model.File>? Files      { get; set; }
        public PaginatedList<Folder>?               Folders     { get; set; }
        public PaginatedList<Fileversion>?          Commits     { get; set; }
        public PaginatedList<Tag>?                  Tags        { get; set; }

        public int TotalCount { get; set; }
    }
}
