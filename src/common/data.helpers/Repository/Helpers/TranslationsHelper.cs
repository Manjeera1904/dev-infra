using EI.API.Service.Data.Helpers.Model;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace EI.API.Service.Data.Helpers.Repository.Helpers;

public static class TranslationsHelper
{
    private static ConcurrentDictionary<string, ISet<string>> CultureCodeSearches { get; }
        = new(StringComparer.OrdinalIgnoreCase);

    public static ISet<string> GetCultureCodeSet(string cultureCode)
    {
        var translationsExpression =
            CultureCodeSearches.GetOrAdd(cultureCode,
                                          c =>
                                          {
                                              var cultureCodes = new HashSet<string> { ServiceConstants.CultureCode.Default, c };
                                              return cultureCodes;
                                          });
        return translationsExpression;
    }

    [return: NotNullIfNotNull(nameof(entity))]
    public static TEntity? SelectBestTranslation<TEntity, TTranslation>(TEntity? entity, IList<TTranslation> translations, string preferredCultureCode)
        where TEntity : BaseDatabaseEntityWithTranslation<TTranslation>
        where TTranslation : BaseDatabaseTranslationsEntity<TEntity>
    {
        if (entity == null || translations.Count == 0)
        {
            return entity;
        }

        TTranslation? bestSoFar = null;

        foreach (var translation in translations)
        {
            if (translation.CultureCode.Equals(preferredCultureCode, StringComparison.InvariantCultureIgnoreCase))
            {
                bestSoFar = translation;
                break;
            }

            if (translation.CultureCode.Equals(ServiceConstants.CultureCode.Default, StringComparison.InvariantCultureIgnoreCase))
            {
                // If we haven't already found something, use the default culture
                bestSoFar ??= translation;
            }
        }

        entity.Translations = bestSoFar == null ? [] : [bestSoFar];

        return entity;
    }
}
