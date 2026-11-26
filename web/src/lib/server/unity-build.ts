import "server-only";
import { access } from "node:fs/promises";
import path from "node:path";

/**
 * Where the Unity WebGL build lives. Default: /public/unity/Build/NFWL.* in this app.
 * In production point NEXT_PUBLIC_UNITY_BUILD_URL at S3-compatible storage / CDN.
 */
export async function unityBuildInfo() {
  const baseUrl = (process.env.NEXT_PUBLIC_UNITY_BUILD_URL ?? "/unity/Build").replace(/\/$/, "");
  const name = process.env.NEXT_PUBLIC_UNITY_BUILD_NAME ?? "NFWL";
  // "unityweb" = Brotli with decompression fallback (default of NFWL > Build WebGL); "br"/"gz" need Content-Encoding headers.
  const c = process.env.NEXT_PUBLIC_UNITY_COMPRESSION ?? "unityweb";
  const ext = c === "br" ? ".br" : c === "gz" ? ".gz" : c === "unityweb" ? ".unityweb" : "";
  let available = false;
  if (baseUrl.startsWith("/")) {
    available = await access(path.join(process.cwd(), "public", baseUrl, `${name}.loader.js`)).then(() => true, () => false);
  } else {
    available = await fetch(`${baseUrl}/${name}.loader.js`, { method: "HEAD", signal: AbortSignal.timeout(3000) }).then((r) => r.ok, () => false);
  }
  return { baseUrl, name, ext, available };
}
