using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PayLite.WalletApi;

/// <summary>Query string for paged lists: <c>?page=1&amp;pageSize=20</c> (names from the brief).</summary>
public class PageRequestDto
{
    // Explicit names so Swagger (and the generated frontend types) show ?page=&pageSize=, not Page/PageSize.
    [FromQuery(Name = "page")]
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;

    [FromQuery(Name = "pageSize")]
    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = 20;
}

public class PagedResponseDto<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
