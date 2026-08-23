namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// Deterministic 768-dim embedding derived from the 64 perceptual-hash bits
/// (bit i%64 decides the sign of dimension i, unit-normalized).
/// NOT a semantic embedding: it is a fingerprint whose cosine similarity
/// correlates with hash agreement. Retrieval relies primarily on pHash
/// Hamming distance + trigram text + structured filters; the pgvector column
/// and HNSW index exist so real model embeddings can land later without a
/// schema change.
/// </summary>
public static class EmbeddingService
{
    /// <summary>The vector dimensionality (matches the vector(768) column).</summary>
    public const int Dimensions = 768;

    /// <summary>Builds the deterministic unit vector for a perceptual hash.</summary>
    public static float[] FromHash(ulong perceptualHash)
    {
        var vector = new float[Dimensions];
        var norm = MathF.Sqrt(Dimensions);
        for (var i = 0; i < Dimensions; i++)
        {
            var bit = (perceptualHash >> (i % 64)) & 1UL;
            vector[i] = (bit == 1UL ? 1f : -1f) / norm;
        }

        return vector;
    }
}
