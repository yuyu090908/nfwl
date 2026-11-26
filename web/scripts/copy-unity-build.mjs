// Copies the Unity WebGL output (unity/NFWL/Builds/NFWL/Build) into public/unity/Build for local use.
// Usage: npm run unity:copy [-- <path to the Build folder>]
import { cpSync, existsSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";

const src = resolve(process.argv[2] ?? "../unity/NFWL/Builds/NFWL/Build");
const dest = resolve("public/unity/Build");
if (!existsSync(src)) {
  console.error(`Build folder not found: ${src}\nRun "NFWL > Build WebGL" in Unity first.`);
  process.exit(1);
}
mkdirSync(dest, { recursive: true });
cpSync(src, dest, { recursive: true });
console.log(`Copied ${src} -> ${dest}`);
