import { Slot } from "radix-ui";
import { cva, type VariantProps } from "class-variance-authority";
import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

export const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl font-display font-semibold uppercase tracking-wider transition-all duration-200 select-none disabled:pointer-events-none disabled:opacity-45 active:scale-[0.98] focus-visible:outline-2 focus-visible:outline-crystal",
  {
    variants: {
      variant: {
        gold: "bg-gradient-to-b from-[#f6d27c] to-gold text-[#1a1306] shadow-gold hover:brightness-110 hover:-translate-y-px",
        crystal: "bg-gradient-to-b from-[#7ae6ff] to-crystal text-[#04121a] shadow-glow hover:brightness-110 hover:-translate-y-px",
        outline: "border border-line bg-card/60 text-ink hover:border-crystal/60 hover:bg-card-2",
        ghost: "text-ink-2 hover:text-ink hover:bg-card-2",
        danger: "bg-danger/90 text-white hover:bg-danger",
      },
      size: {
        sm: "h-9 px-3 text-xs",
        md: "h-11 px-5 text-sm",
        lg: "h-14 px-8 text-base",
        icon: "h-10 w-10",
      },
    },
    defaultVariants: { variant: "outline", size: "md" },
  },
);

export function Button({
  className,
  variant,
  size,
  asChild,
  ...props
}: ComponentProps<"button"> & VariantProps<typeof buttonVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "button";
  return <Comp className={cn(buttonVariants({ variant, size }), className)} {...props} />;
}
