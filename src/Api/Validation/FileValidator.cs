using Api.Dtos;

namespace Api.Validation;

public class FileValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xaml", ".cs", ".resx", ".csproj", ".sln", ".config", ".json", ".xml", ".razor", ".css"
    };

    private readonly int _maxFileBytes;
    private readonly int _maxFilesPerJob;

    public FileValidator(int maxFileBytes, int maxFilesPerJob)
    {
        _maxFileBytes = maxFileBytes;
        _maxFilesPerJob = maxFilesPerJob;
    }

    public void Validate(List<FileInput> files)
    {
        if (files == null || files.Count == 0)
            throw new Middleware.ValidationException("At least one file is required.");

        if (files.Count > _maxFilesPerJob)
            throw new Middleware.ValidationException($"Maximum {_maxFilesPerJob} files per job.");

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Path))
                throw new Middleware.ValidationException("File path cannot be empty.");

            var sanitized = file.Path.Replace("\\", "/");
            if (sanitized.Contains("..") || Path.IsPathRooted(sanitized))
                throw new Middleware.ValidationException($"Invalid file path: {file.Path}");

            var ext = Path.GetExtension(file.Path);
            if (!AllowedExtensions.Contains(ext))
                throw new Middleware.ValidationException($"File extension not allowed: {ext}");

            if (string.IsNullOrEmpty(file.Content))
                throw new Middleware.ValidationException($"File content cannot be empty: {file.Path}");

            if (System.Text.Encoding.UTF8.GetByteCount(file.Content) > _maxFileBytes)
                throw new Middleware.ValidationException($"File exceeds max size ({_maxFileBytes} bytes): {file.Path}");
        }
    }
}
