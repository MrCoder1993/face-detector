using OpenCvSharp;

namespace Recognition;

/// <summary>
/// محاسبه‌ی هش ادراکی (Perceptual Hash) از نوع dHash برای مقایسه‌ی شباهت تصاویر.
/// dHash با مقایسه‌ی روشنایی پیکسل‌های مجاور در نسخه‌ی کوچک‌شده‌ی تصویر کار می‌کند.
/// </summary>
public static class PerceptualHash
{
    /// <summary>
    /// عرض تصویر در الگوریتم dHash.
    /// (۹ ستون تا بتوان بین هر دو ستون مجاور، ۸ مقایسه انجام داد)
    /// </summary>
    public const int DHashWidth = 10;

    /// <summary>
    /// ارتفاع تصویر در الگوریتم dHash.
    /// </summary>
    public const int DHashHeight = 9;

    /// <summary>
    /// محاسبه‌ی dHash برای تصویر ورودی (چهره) در فضای رنگی BGR.
    /// </summary>
    /// <param name="bgrFace">تصویر ورودی در قالب `Mat` و فضای رنگی BGR.</param>
    /// <returns>
    /// مقدار هش ۶۴-بیتی (۸×۸ مقایسه). هر بیت نشان می‌دهد پیکسل چپ روشن‌تر از پیکسل راست بوده است یا نه.
    /// در صورت خالی بودن تصویر، مقدار ۰ برگردانده می‌شود.
    /// </returns>
    public static ulong ComputeDHash(Mat bgrFace)
    {
        // جلوگیری از ارسال مقدار null
        if (bgrFace is null) throw new ArgumentNullException(nameof(bgrFace));

        // اگر تصویر خالی باشد، هش معنی‌داری نداریم
        if (bgrFace.Empty()) return 0;

        // تبدیل به خاکستری برای حذف اثر رنگ و تمرکز روی روشنایی
        using var gray = new Mat();
        Cv2.CvtColor(bgrFace, gray, ColorConversionCodes.BGR2GRAY);

        // تغییر اندازه به ۹×۸ (برای تولید ۸×۸ مقایسه‌ی افقی)
        using var resized = new Mat();
        Cv2.Resize(gray, resized, new Size(DHashWidth, DHashHeight));

        // ساخت هش با قرار دادن بیت‌ها بر اساس نتیجه‌ی مقایسه‌ی پیکسل‌های مجاور
        ulong hash = 0;
        var bit = 0;

        for (var y = 0; y < DHashHeight; y++)
        {
            for (var x = 0; x < DHashWidth - 1; x++)
            {
                // خواندن مقدار روشنایی پیکسل چپ و راست
                var left = resized.At<byte>(y, x);
                var right = resized.At<byte>(y, x + 1);

                // اگر پیکسل چپ روشن‌تر باشد، بیت مربوطه را ۱ می‌کنیم
                if (left > right)
                    hash |= 1UL << bit;

                bit++;
            }
        }

        return hash;
    }

    /// <summary>
    /// محاسبه‌ی فاصله‌ی همینگ بین دو هش ۶۴-بیتی.
    /// (تعداد بیت‌هایی که با هم متفاوت هستند)
    /// </summary>
    public static int HammingDistance(ulong a, ulong b)
    {
        // XOR بیت‌های متفاوت را ۱ می‌کند
        var x = a ^ b;

        // شمارش تعداد بیت‌های ۱ با روش Kernighan
        var count = 0;
        while (x != 0)
        {
            x &= x - 1;
            count++;
        }

        return count;
    }
}