namespace Microsoft.Azure.Cosmos.Linq;

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

/// <summary>
/// Provides Cosmos Linq Query Extension methods
/// </summary>
public static class CosmosLinqQueryExtensions
{
    private static MethodInfo? _whereOrTSource2;

    private static MethodInfo WhereOr_TSource_2(Type TSource) =>
        (_whereOrTSource2 ??= new Func<IQueryable<object>, Expression<Func<object, bool>>, IQueryable<object>>(WhereOr).GetMethodInfo().GetGenericMethodDefinition())
        .MakeGenericMethod(TSource);
        
    /// <summary>
    /// Performs a logical OR of the current Expression along with the provided Predicate.
    /// </summary>
    /// <param name="source">The list of items</param>
    /// <param name="predicate">The Expression to append</param>
    /// <typeparam name="TSource">Source Query Type</typeparam>
    /// <returns>The result of applying the predicate as an OR</returns>
    public static IQueryable<TSource> WhereOr<TSource>(this IQueryable<TSource> source, Expression<Func<TSource,bool>> predicate) => 
        source.Provider.CreateQuery<TSource>(
            Expression.Call(
            null,
            WhereOr_TSource_2(typeof(TSource)),
            source.Expression, Expression.Quote(predicate)));

}