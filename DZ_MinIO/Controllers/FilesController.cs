using DZ_MinIO.Models;
using DZ_MinIO.Services;
using Microsoft.AspNetCore.Mvc;

namespace DZ_MinIO.Controllers;

public class FilesController : Controller
{
    private readonly IFileService _fileService;
    private readonly IConfiguration _config;
    private readonly ILogger<FilesController> _logger;
    private readonly string[] _allowedExt;
    private readonly long _maxSize;

    public FilesController(IFileService fileService,
                           IConfiguration config,
                           ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _config = config;
        _logger = logger;

        var validation = config.GetSection("FileValidation");
        _allowedExt = validation.GetSection("AllowedExtensions").Get<string[]>()
                      ?? Array.Empty<string>();
        _maxSize = validation.GetValue<long>("MaxFileSizeMb") * 1024 * 1024;
    }

    /// <summary>
    /// Отображение списка файлов в бакете
    /// </summary>
    public async Task<IActionResult> Index()
    {
        try
        {
            var files = await _fileService.ListAsync();
            return View(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка файлов");
            return View("Error");
        }
    }

    /// <summary>
    /// Форма загрузки (GET)
    /// </summary>
    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    /// <summary>
    /// Обработка загрузки (POST)
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(11_000_000)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Файл не выбран или пуст");
            return View();
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExt.Contains(ext))
        {
            ModelState.AddModelError("file",
                $"Недопустимый тип файла. Разрешены: {string.Join(", ", _allowedExt)}");
            return View();
        }

        if (file.Length > _maxSize)
        {
            ModelState.AddModelError("file",
                $"Размер файла превышает {_maxSize / 1024 / 1024} MB");
            return View();
        }

        try
        {
            await _fileService.UploadAsync(file);
            TempData["Message"] = $"Файл {file.FileName} загружен успешно";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке файла");
            ModelState.AddModelError("", "Внутренняя ошибка сервера");
            return View();
        }
    }

    /// <summary>
    /// Скачивание файла по имени
    /// </summary>
    public async Task<IActionResult> Download(string fileName)
    {
        try
        {
            var stream = await _fileService.DownloadAsync(fileName);
            return File(stream, "application/octet-stream", fileName);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return NotFound($"Файл '{fileName}' не найден");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка скачивания {FileName}", fileName);
            return StatusCode(500, "Внутренняя ошибка сервера");
        }
    }

    /// <summary>
    /// Удаление файла
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string fileName)
    {
        try
        {
            await _fileService.DeleteAsync(fileName);
            TempData["Message"] = $"Файл '{fileName}' удалён";
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            TempData["Error"] = $"Файл '{fileName}' не найден";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления файла {FileName}", fileName);
            TempData["Error"] = "Внутренняя ошибка сервера";
        }

        return RedirectToAction(nameof(Index));
    }
}