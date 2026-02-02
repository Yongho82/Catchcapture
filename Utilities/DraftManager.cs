using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CatchCapture.Resources;

namespace CatchCapture.Utilities
{
    public class DraftData
    {
        public string? FileName { get; set; } // 삭제 시 필요
        public long? OriginalNoteId { get; set; }
        public string Title { get; set; } = "";
        public string ContentXaml { get; set; } = "";
        public string Tags { get; set; } = "";
        public long CategoryId { get; set; } = 1;
        public DateTime SavedAt { get; set; }
        
        // 디스플레이용
        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? LocalizationManager.GetString("NoTitle") : Title;
        public string DisplayTime => SavedAt.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static class DraftManager
    {
        private static readonly string DraftsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "CatchCapture", "Drafts");

        static DraftManager()
        {
            if (!Directory.Exists(DraftsFolder))
            {
                Directory.CreateDirectory(DraftsFolder);
            }
        }

        public static void SaveDraft(DraftData data)
        {
            try
            {
                data.SavedAt = DateTime.Now;
                string fileName = $"draft_{DateTime.Now.Ticks}.json";
                string filePath = Path.Combine(DraftsFolder, fileName);
                data.FileName = fileName;

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);

                CleanupOldDrafts();
            }
            catch { }
        }

        public static List<DraftData> GetDraftList()
        {
            var list = new List<DraftData>();
            try
            {
                var files = Directory.GetFiles(DraftsFolder, "draft_*.json")
                                     .OrderByDescending(f => f)
                                     .Take(10); // 최대 10개로 증가

                foreach (var file in files)
                {
                    string json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<DraftData>(json);
                    if (data != null)
                    {
                        data.FileName = Path.GetFileName(file);
                        list.Add(data);
                    }
                }
            }
            catch { }
            return list;
        }

        public static void DeleteDraft(string fileName)
        {
            try
            {
                string filePath = Path.Combine(DraftsFolder, fileName);
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { }
        }

        public static void DeleteAllDrafts()
        {
            try
            {
                foreach (var file in Directory.GetFiles(DraftsFolder, "draft_*.json"))
                {
                    File.Delete(file);
                }
            }
            catch { }
        }

        public static void CleanupOldDrafts()
        {
            try
            {
                var files = Directory.GetFiles(DraftsFolder, "draft_*.json")
                                     .OrderByDescending(f => f)
                                     .Skip(10) // 10개만 유지
                                     .ToList();

                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            catch { }
        }

        public static int GetDraftCount()
        {
            try
            {
                return Directory.GetFiles(DraftsFolder, "draft_*.json").Length;
            }
            catch { return 0; }
        }
    }
}
