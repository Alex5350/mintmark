import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Workspace packages ship TypeScript source; transpile them into the app.
  transpilePackages: ["@mintmark/ui-tokens", "@mintmark/domain-types", "@mintmark/api-client"],
};

export default nextConfig;
