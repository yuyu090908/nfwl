import type { NextConfig } from "next";

const isProd = process.env.NODE_ENV === "production";
// Unity WebGL needs 'wasm-unsafe-eval'; the build can be served from a CDN listed in NEXT_PUBLIC_UNITY_BUILD_URL.
const unityOrigin = (() => {
  try {
    return process.env.NEXT_PUBLIC_UNITY_BUILD_URL ? new URL(process.env.NEXT_PUBLIC_UNITY_BUILD_URL).origin : "";
  } catch {
    return "";
  }
})();

const csp = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' ${isProd ? "" : "'unsafe-eval'"} ${unityOrigin} blob:`,
  "style-src 'self' 'unsafe-inline'",
  `img-src 'self' data: blob: ${unityOrigin}`,
  "font-src 'self' data:",
  `connect-src 'self' ${unityOrigin} blob: data:`,
  "worker-src 'self' blob:",
  "media-src 'self' blob:",
  "frame-ancestors 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "object-src 'none'",
].join("; ");

const nextConfig: NextConfig = {
  poweredByHeader: false,
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async headers() {
    return [
      {
        source: "/:path*",
        headers: [
          { key: "Content-Security-Policy", value: csp.replace(/\s+/g, " ") },
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), fullscreen=(self)" },
          ...(isProd ? [{ key: "Strict-Transport-Security", value: "max-age=63072000; includeSubDomains; preload" }] : []),
        ],
      },
      // Unity WebGL build files served from /public/unity: long cache, correct encodings for pre-compressed files.
      { source: "/unity/:path*.br", headers: [{ key: "Content-Encoding", value: "br" }, { key: "Cache-Control", value: "public, max-age=31536000, immutable" }] },
      { source: "/unity/:path*.gz", headers: [{ key: "Content-Encoding", value: "gzip" }, { key: "Cache-Control", value: "public, max-age=31536000, immutable" }] },
      { source: "/unity/:path*.wasm.br", headers: [{ key: "Content-Type", value: "application/wasm" }] },
      { source: "/unity/:path*.wasm.gz", headers: [{ key: "Content-Type", value: "application/wasm" }] },
      { source: "/unity/:path*.js.br", headers: [{ key: "Content-Type", value: "application/javascript" }] },
      { source: "/unity/:path*.js.gz", headers: [{ key: "Content-Type", value: "application/javascript" }] },
    ];
  },
};

export default nextConfig;
