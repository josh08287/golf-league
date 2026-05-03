namespace GolfLeague.Application.Common;

public sealed class PageMeta
{
    public int Page { get; }
    public int PageSize { get; }
    public int TotalCount { get; }

    public PageMeta(int page, int pageSize, int totalCount)
    {
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
}

public sealed class PagedResult<T>
{
    public List<T> Data { get; }
    public PageMeta Meta { get; }
    public List<string> Errors { get; } = [];

    public PagedResult(List<T> data, int page, int pageSize, int totalCount)
    {
        Data = data;
        Meta = new PageMeta(page, pageSize, totalCount);
    }
}
