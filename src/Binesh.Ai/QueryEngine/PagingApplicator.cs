namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Trivial Skip/Take wrapper kept as its own type so the query pipeline reads
/// uniformly (validate → include → filter → order → page).
/// </summary>
public static class PagingApplicator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, AiPaging? paging)
    {
        if (paging is null) return query;
        if (paging.Skip > 0) query = query.Skip(paging.Skip);
        if (paging.Take > 0) query = query.Take(paging.Take);
        return query;
    }
}
