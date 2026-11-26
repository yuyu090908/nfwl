import type { Metadata, Viewport } from "next";
import { Chakra_Petch, Inter } from "next/font/google";
import "./globals.css";
import { SiteHeader } from "@/components/layout/site-header";
import { SiteFooter } from "@/components/layout/site-footer";
import { MobileTabBar } from "@/components/layout/header-client";

const inter = Inter({ variable: "--font-inter", subsets: ["latin"], display: "swap" });
const chakra = Chakra_Petch({ variable: "--font-chakra", subsets: ["latin"], weight: ["500", "600", "700"], display: "swap" });

export const metadata: Metadata = {
  title: { default: "NFWL — NFT Football World League", template: "%s | NFWL" },
  description: "Build your club. Rule the world. 5v5 football with Crystalia players, world divisions and the Champions League.",
};

export const viewport: Viewport = { themeColor: "#080b12", width: "device-width", initialScale: 1 };

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ja" className={`${inter.variable} ${chakra.variable} h-full antialiased`}>
      <body className="flex min-h-full flex-col">
        <SiteHeader />
        <main id="main" className="flex-1">{children}</main>
        <SiteFooter />
        <MobileTabBar />
      </body>
    </html>
  );
}
