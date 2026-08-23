/**
 * The API signs presigned image URLs against its configured public base
 * (https://localhost:9000 in the dev environment), but the local MinIO
 * serves plain http. The S3 v4 signature covers the host header only, so
 * swapping the scheme keeps the URL valid. Presentation-edge concern — the
 * DTOs pass through untouched.
 */
export function presignedImageUrl(url: string | null | undefined): string | null {
  if (!url) return null;
  if (url.startsWith("https://localhost:9000/")) {
    return url.replace("https://", "http://");
  }
  return url;
}
