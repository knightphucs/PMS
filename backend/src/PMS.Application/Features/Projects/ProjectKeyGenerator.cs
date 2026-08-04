using System.Globalization;
using System.Text;

namespace PMS.Application.Features.Projects;

/// <summary>
/// Sinh mã ngắn cho project từ tên (<c>"Hệ thống kho"</c> → <c>"HTK"</c>) — nửa đầu của mã
/// task <c>PMS-12</c> (ADR-033). Người dùng không nhập mã, tránh phải thêm một trường form
/// và một luật validate nữa cho thứ họ không quan tâm.
/// <para>
/// Bóc dấu tiếng Việt bằng <see cref="NormalizationForm.FormD"/> + lọc
/// <see cref="UnicodeCategory.NonSpacingMark"/> — cách này xử lý đúng cả ký tự tổ hợp
/// (<c>ế ệ ỗ ữ</c>). Riêng <c>đ/Đ</c> phải map tay: nó là một ký tự Latin độc lập, không
/// phải chữ cái kèm dấu, nên FormD không tách được gì.
/// </para>
/// </summary>
public static class ProjectKeyGenerator
{
    public const int MaxLength = 10;
    private const string Fallback = "PRJ";

    /// <summary>
    /// Ứng viên mã, KHÔNG bảo đảm duy nhất — người gọi phải kiểm trùng và thêm hậu tố số.
    /// Luôn trả về chuỗi không rỗng.
    /// </summary>
    public static string FromName(string name)
    {
        var words = Deaccent(name)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .ToArray();

        if (words.Length == 0) return Fallback;

        // Nhiều từ → lấy chữ cái đầu mỗi từ ("Hệ thống kho" → HTK).
        // Một từ    → lấy tối đa 3 ký tự đầu ("Payments" → PAY).
        var candidate = words.Length > 1
            ? new string(words.Select(w => w[0]).ToArray())
            : words[0][..Math.Min(3, words[0].Length)];

        candidate = candidate.ToUpperInvariant();
        return candidate.Length == 0 ? Fallback
             : candidate[..Math.Min(candidate.Length, MaxLength)];
    }

    /// <summary>
    /// Ghép hậu tố số vào mã gốc mà vẫn tôn trọng <see cref="MaxLength"/> — cắt bớt phần
    /// gốc chứ không cắt hậu tố, nếu không thì "HTK10" và "HTK1" có thể va nhau sau khi cắt.
    /// <c>attempt = 1</c> trả về chính mã gốc.
    /// </summary>
    public static string WithSuffix(string baseKey, int attempt)
    {
        if (attempt <= 1) return baseKey;

        var suffix = attempt.ToString(CultureInfo.InvariantCulture);
        var room = Math.Max(1, MaxLength - suffix.Length);
        return string.Concat(baseKey[..Math.Min(baseKey.Length, room)], suffix);
    }

    private static string Deaccent(string input)
    {
        var mapped = input.Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = mapped.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
