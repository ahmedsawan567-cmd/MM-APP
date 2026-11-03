namespace MMApplication.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName);
    Task<bool> DeleteFileAsync(string relativeFilePath);
}
