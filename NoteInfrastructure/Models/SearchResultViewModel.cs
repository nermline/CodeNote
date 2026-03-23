using NoteDomain.Model;

namespace NoteInfrastructure.Models
{
    public class SearchResultViewModel
    {
        public string Query { get; set; } = "";
        public List<NoteDomain.Model.File> Files { get; set; } = new();
        public List<Folder> Folders { get; set; } = new();
        public List<Fileversion> Commits { get; set; } = new();
        public List<Tag> Tags { get; set; } = new();

        public int TotalCount => Files.Count + Folders.Count + Commits.Count + Tags.Count;
    }
}
