using System;

namespace CatchCapture.Models
{
    public class NoteAttachment
    {
        public long Id { get; set; }
        public long NoteId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string OriginalName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }
}
