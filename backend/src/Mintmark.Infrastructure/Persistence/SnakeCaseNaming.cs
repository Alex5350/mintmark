using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mintmark.Infrastructure.Persistence;

/// <summary>
/// snake_case naming for every table, column, key, foreign key and index.
/// Implemented once here (there is no maintained EFCore.NamingConvention
/// package for EF 10 — see docs/open-questions.md) and applied at the end of
/// <see cref="MintmarkDbContext.OnModelCreating"/> so it also covers the
/// ASP.NET Identity tables configured by the base context. Explicit
/// <c>.ToTable/.HasColumnName</c> calls would drift; this walker cannot.
/// </summary>
public static class SnakeCaseNaming
{
    /// <summary>Renames the whole model (tables, columns, keys, FKs, indexes) to snake_case.</summary>
    public static void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            entityType.SetTableName(ToSnakeCase(tableName));
            var table = StoreObjectIdentifier.Table(
                entityType.GetTableName()!, entityType.GetSchema());

            RenameProperties(entityType.GetProperties(), table);

            // Complex properties (Money value objects) contribute their own
            // scalar columns named "ComplexProperty_Scalar"; rename those too.
            RenameComplexProperties(entityType.GetComplexProperties(), table);

            foreach (var key in entityType.GetKeys())
            {
                if (key.GetName() is { } name)
                {
                    key.SetName(ToSnakeCase(name));
                }
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                if (foreignKey.GetConstraintName() is { } name)
                {
                    foreignKey.SetConstraintName(ToSnakeCase(name));
                }
            }

            foreach (var index in entityType.GetIndexes())
            {
                if (index.GetDatabaseName() is { } name)
                {
                    index.SetDatabaseName(ToSnakeCase(name));
                }
            }
        }
    }

    private static void RenameComplexProperties(IEnumerable<IMutableComplexProperty> properties, in StoreObjectIdentifier table)
    {
        foreach (var complex in properties)
        {
            RenameProperties(complex.ComplexType.GetProperties(), table);
            RenameComplexProperties(complex.ComplexType.GetComplexProperties(), table);
        }
    }

    private static void RenameProperties(IEnumerable<IMutableProperty> properties, in StoreObjectIdentifier table)
    {
        foreach (var property in properties)
        {
            if (property.GetColumnName(table) is { } column)
            {
                property.SetColumnName(ToSnakeCase(column));
            }
        }
    }

    /// <summary>
    /// Converts a PascalCase (or Pascal_Snake mixed) identifier to snake_case.
    /// Handles acronym runs: <c>AspNetUsers</c> → <c>asp_net_users</c>.
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (!char.IsUpper(c))
            {
                _ = builder.Append(c);
                continue;
            }

            var followsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
            var acronymBoundary = i > 0 && char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]);
            if (i > 0 && (followsLowerOrDigit || acronymBoundary))
            {
                _ = builder.Append('_');
            }

            _ = builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
