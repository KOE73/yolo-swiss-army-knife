using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YoloHelperApp.Services;

public class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private AppSettingsService? _appSettings;

    /// <summary>Loads the persisted language and keeps future changes saved to app settings.</summary>
    public void Initialize(AppSettingsService appSettings)
    {
        _appSettings = appSettings;
        CurrentLanguage = appSettings.Settings.Language;
    }

    private string _currentLanguage = "RU";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value)
            {
                _currentLanguage = value;
                OnPropertyChanged();
                OnPropertyChanged("Item"); // Trigger refresh for all localized strings

                if (_appSettings != null && _appSettings.Settings.Language != value)
                {
                    _appSettings.Settings.Language = value;
                    _appSettings.Save();
                }
            }
        }
    }

    public string this[string key] => GetString(key);

    private string GetString(string key)
    {
        return CurrentLanguage == "RU" ? GetRussianString(key) : GetEnglishString(key);
    }

    private string GetRussianString(string key) => key switch
    {
        "Aug_min_width_desc" => "Минимальная ширина bbox (в пикселях), при которой объект включается в обучение.\nЕдиницы: пиксели (до ресайза imgsz). Зачем: при меньших ширинах текст и структура мешков теряются, OCR не сработает.",
        "Aug_min_height_desc" => "Минимальная высота bbox (в пикселях), при которой паллета считается \"полноценной\".\nНиже этого значения структура мешков размыта, OCR или keypoints неосмысленны. Это фильтрует боксы, в которых паллета “слишком далеко” или “обрезана”.",
        "Aug_hsv_h_desc" => "Изменение оттенка (hue) изображения в пространстве HSV.\nЕдиницы: Доля от полного диапазона (0–1 соответствует 0–360°). Диапазон: 0.0–1.0.",
        "Aug_hsv_s_desc" => "Изменение насыщенности (saturation) в пространстве HSV.\nЕдиницы: Доля от полной насыщенности (0–1). Диапазон: 0.0–1.0.",
        "Aug_hsv_v_desc" => "Изменение яркости (value) в пространстве HSV.\nЕдиницы: Доля от полной яркости (0–1). Диапазон: 0.0–1.0.",
        "Aug_degrees_desc" => "Случайный поворот изображения.\nЕдиницы: Градусы. Диапазон: -180.0–180.0.",
        "Aug_translate_desc" => "Случайный сдвиг изображения по горизонтали и вертикали.\nЕдиницы: Доля от размера (0–1). Диапазон: 0.0–1.0.",
        "Aug_scale_desc" => "Случайное масштабирование (увеличение/уменьшение) изображения.\nЕдиницы: Доля от размера (0–1 добавляется к 1.0). Диапазон: 0.0–1.0.",
        "Aug_shear_desc" => "Случайный наклон (сдвиг перспективы) изображения.\nЕдиницы: Градусы. Диапазон: -180.0–180.0.",
        "Aug_perspective_desc" => "Случайное изменение перспективы (имитация искажения камеры).\nЕдиницы: Доля от размера (0–1). Диапазон: 0.0–0.001.",
        "Aug_flipud_desc" => "Вероятность вертикального переворота изображения (сверху вниз).\nЕдиницы: Вероятность (0–1).",
        "Aug_fliplr_desc" => "Вероятность горизонтального переворота изображения (слева направо).\nЕдиницы: Вероятность (0–1).",
        "Aug_mosaic_desc" => "Вероятность применения мозаики (объединение 4 изображений в одно).\nЕдиницы: Вероятность (0–1).",
        "Aug_mixup_desc" => "Вероятность применения MixUp (наложение двух изображений с прозрачностью).\nЕдиницы: Вероятность (0–1).",
        "Aug_copy_paste_desc" => "Вероятность применения Copy-Paste (копирование объектов с одного изображения на другое).\nЕдиницы: Вероятность (0–1).",
        _ => $"[{key}]"
    };

    private string GetEnglishString(string key) => key switch
    {
        "Aug_min_width_desc" => "Minimum bbox width (in pixels) for an object to be included in training.\nUnits: pixels (before imgsz resize). Why: at smaller widths text and bag structures are lost, OCR will fail.",
        "Aug_min_height_desc" => "Minimum bbox height (in pixels) for a pallet to be considered \"full\".\nBelow this, bag structures are blurred, OCR or keypoints are meaningless. Filters out boxes that are \"too far\" or \"cropped\".",
        "Aug_hsv_h_desc" => "Image hue alteration (HSV).\nUnits: Fraction of full range (0-1 maps to 0-360°). Range: 0.0-1.0.",
        "Aug_hsv_s_desc" => "Image saturation alteration (HSV).\nUnits: Fraction of full saturation (0-1). Range: 0.0-1.0.",
        "Aug_hsv_v_desc" => "Image value/brightness alteration (HSV).\nUnits: Fraction of full brightness (0-1). Range: 0.0-1.0.",
        "Aug_degrees_desc" => "Random image rotation.\nUnits: Degrees. Range: -180.0-180.0.",
        "Aug_translate_desc" => "Random horizontal and vertical image translation.\nUnits: Fraction of size (0-1). Range: 0.0-1.0.",
        "Aug_scale_desc" => "Random image scaling (zoom in/out).\nUnits: Fraction of size (0-1 added to 1.0). Range: 0.0-1.0.",
        "Aug_shear_desc" => "Random image shear (perspective shift).\nUnits: Degrees. Range: -180.0-180.0.",
        "Aug_perspective_desc" => "Random perspective alteration (simulating camera distortion).\nUnits: Fraction of size (0-1). Range: 0.0-0.001.",
        "Aug_flipud_desc" => "Probability of vertical image flip (upside down).\nUnits: Probability (0-1).",
        "Aug_fliplr_desc" => "Probability of horizontal image flip (left to right).\nUnits: Probability (0-1).",
        "Aug_mosaic_desc" => "Probability of applying mosaic (combining 4 images into one).\nUnits: Probability (0-1).",
        "Aug_mixup_desc" => "Probability of applying MixUp (overlaying two images with transparency).\nUnits: Probability (0-1).",
        "Aug_copy_paste_desc" => "Probability of applying Copy-Paste (copying objects from one image to another).\nUnits: Probability (0-1).",
        _ => $"[{key}]"
    };

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
