namespace APDS.Services
{
    public class FileStorageSettings
    {
        public string RootPath { get; set; } = "App_Data/Uploads/Activities";
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
        public string[] AllowedExtensions { get; set; } = { ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".doc" };
    }
}