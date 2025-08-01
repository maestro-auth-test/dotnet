﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.Cosmos.Metadata.Internal;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Index extension methods for Azure Cosmos DB-specific metadata.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see>, and
///     <see href="https://aka.ms/efcore-docs-cosmos">Accessing Azure Cosmos DB with EF Core</see> for more information and examples.
/// </remarks>
public static class CosmosIndexExtensions
{
    /// <summary>
    ///     Returns the vector index type to use, such as "flat", "diskANN", or "quantizedFlat".
    ///     See <see href="https://aka.ms/ef-cosmos-vectors">Vector Search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The index type to use, or <see langword="null" /> if none is set.</returns>
    public static VectorIndexType? GetVectorIndexType(this IReadOnlyIndex index)
        => (index is RuntimeIndex)
            ? throw new InvalidOperationException(CoreStrings.RuntimeModelMissingData)
            : (VectorIndexType?)index[CosmosAnnotationNames.VectorIndexType];

    /// <summary>
    ///     Sets the vector index type to use, such as "flat", "diskANN", or "quantizedFlat".
    ///     See <see href="https://aka.ms/ef-cosmos-vectors">Vector Search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="indexType">The index type to use.</param>
    /// <param name="vectorIndex">The value indicating whether the index is configured as a vector index.</param>
    public static void SetVectorIndexType(this IMutableIndex index, VectorIndexType? indexType, bool? vectorIndex)
    {
        if (vectorIndex == true)
        {
            index.SetAnnotation(CosmosAnnotationNames.VectorIndexType, indexType);
        }
        else
        {
            index.RemoveAnnotation(CosmosAnnotationNames.VectorIndexType);
        }
    }

    /// <summary>
    ///     Sets the vector index type to use, such as "flat", "diskANN", or "quantizedFlat".
    ///     See <see href="https://aka.ms/ef-cosmos-vectors">Vector Search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="indexType">The index type to use.</param>
    /// <param name="vectorIndex">The value indicating whether the index is configured as a vector index.</param>
    /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
    /// <returns>The configured value.</returns>
    public static string? SetVectorIndexType(
        this IConventionIndex index,
        VectorIndexType? indexType,
        bool? vectorIndex,
        bool fromDataAnnotation = false)
        => (string?)index.SetAnnotation(
            CosmosAnnotationNames.VectorIndexType,
            indexType,
            fromDataAnnotation)?.Value;

    /// <summary>
    ///     Returns the <see cref="ConfigurationSource" /> for whether the <see cref="GetVectorIndexType" />.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The <see cref="ConfigurationSource" /> for whether the index is clustered.</returns>
    public static ConfigurationSource? GetVectorIndexTypeConfigurationSource(this IConventionIndex property)
        => property.FindAnnotation(CosmosAnnotationNames.VectorIndexType)?.GetConfigurationSource();

    /// <summary>
    ///     Returns the value indicating whether the index is configured for full-text search.
    ///     See <see href="https://learn.microsoft.com/azure/cosmos-db/gen-ai/full-text-search">Full-text search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The index type to use, or <see langword="null" /> if none is set.</returns>
    public static bool? IsFullTextIndex(this IReadOnlyIndex index)
        => (index is RuntimeIndex)
            ? throw new InvalidOperationException(CoreStrings.RuntimeModelMissingData)
            : (bool?)index[CosmosAnnotationNames.FullTextIndex];

    /// <summary>
    ///     Configures the index for full-text search.
    ///     See <see href="https://learn.microsoft.com/azure/cosmos-db/gen-ai/full-text-search">Full-text search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="fullTextIndex">The value indicating whether the index is configured for full-text search.</param>
    public static void SetIsFullTextIndex(this IMutableIndex index, bool? fullTextIndex)
        => index.SetAnnotation(CosmosAnnotationNames.FullTextIndex, fullTextIndex);

    /// <summary>
    ///     Configures the index for full-text search.
    ///     See <see href="https://learn.microsoft.com/azure/cosmos-db/gen-ai/full-text-search">Full-text search in Azure Cosmos DB for NoSQL</see> for more information.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="fullTextIndex">The value indicating whether the index is configured for full-text search.</param>
    /// <param name="fromDataAnnotation">Indicates whether the configuration was specified using a data annotation.</param>
    /// <returns>The configured value.</returns>
    public static string? SetIsFullTextIndex(
        this IConventionIndex index,
        bool? fullTextIndex,
        bool fromDataAnnotation = false)
        => (string?)index.SetAnnotation(
            CosmosAnnotationNames.FullTextIndex,
            fullTextIndex,
            fromDataAnnotation)?.Value;

    /// <summary>
    ///     Returns the <see cref="ConfigurationSource" /> for whether the <see cref="IsFullTextIndex" />.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The <see cref="ConfigurationSource" /> for whether the index is clustered.</returns>
    public static ConfigurationSource? GetIsFullTextIndexConfigurationSource(this IConventionIndex property)
        => property.FindAnnotation(CosmosAnnotationNames.FullTextIndex)?.GetConfigurationSource();
}
