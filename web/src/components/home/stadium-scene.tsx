/**
 * Night stadium backdrop drawn in SVG/CSS (no video file yet: an optimised hero video or a
 * Three.js scene can replace this component later without touching the page).
 * Floodlight beams, a perspective pitch and drifting crystal particles; motion stops with
 * prefers-reduced-motion via the global CSS rule.
 */
export function StadiumScene() {
  const particles = Array.from({ length: 22 }, (_, i) => ({
    left: (i * 37) % 100,
    delay: (i * 0.7) % 8,
    dur: 8 + (i % 5) * 2,
    size: 2 + (i % 3),
    color: i % 4 === 0 ? "#f3c65a" : i % 3 === 0 ? "#9258ff" : "#44d9ff",
  }));
  return (
    <div aria-hidden className="absolute inset-0 -z-10 overflow-hidden">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_50%_120%,#0f3a2a_0%,#080b12_60%)]" />
      {/* floodlight beams */}
      {[12, 88].map((x, i) => (
        <div key={x} className="absolute -top-10 h-[120%] w-[45%] origin-top opacity-40 mix-blend-screen"
          style={{ left: `${x - 22}%`, background: "linear-gradient(180deg, rgba(220,240,255,0.55), rgba(68,217,255,0.04) 70%, transparent)", clipPath: "polygon(45% 0, 55% 0, 100% 100%, 0 100%)", transform: `rotate(${i ? -18 : 18}deg)` }} />
      ))}
      {[10, 30, 70, 90].map((x) => (
        <div key={x} className="absolute top-6 h-2 w-14 rounded-full bg-white shadow-[0_0_40px_14px_rgba(220,240,255,0.55)]" style={{ left: `calc(${x}% - 28px)` }} />
      ))}
      {/* crowd band */}
      <div className="absolute inset-x-0 top-[22%] h-[18%] opacity-60" style={{ background: "repeating-linear-gradient(90deg, #1a2234 0 6px, #232d44 6px 9px, #141a28 9px 14px)", maskImage: "linear-gradient(180deg, transparent, black 30%, black 70%, transparent)" }} />
      {/* LED boards */}
      <div className="absolute inset-x-[6%] top-[40%] h-3 rounded bg-gradient-to-r from-crystal/60 via-epic/60 to-gold/60 blur-[1px]" />
      {/* pitch in perspective */}
      <div className="absolute inset-x-0 bottom-0 h-[58%] [perspective:700px]">
        <div className="pitch-grass absolute inset-x-[-25%] bottom-[-30%] top-0 origin-bottom [transform:rotateX(62deg)]">
          <div className="absolute inset-[6%] rounded-sm border-2 border-white/25" />
          <div className="absolute left-1/2 top-[6%] bottom-[6%] w-0.5 -translate-x-1/2 bg-white/25" />
          <div className="absolute left-1/2 top-1/2 h-40 w-40 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white/25" />
        </div>
      </div>
      {/* crystal particles */}
      {particles.map((p, i) => (
        <span key={i} className="absolute bottom-[-10px] rotate-45 animate-[rise_linear_infinite] rounded-[1px] opacity-0"
          style={{ left: `${p.left}%`, width: p.size, height: p.size, background: p.color, boxShadow: `0 0 10px ${p.color}`, animationDuration: `${p.dur}s`, animationDelay: `${p.delay}s` }} />
      ))}
      <div className="absolute inset-0 bg-gradient-to-b from-bg/30 via-transparent to-bg" />
      <style>{`@keyframes rise{0%{transform:translateY(0) rotate(45deg);opacity:0}10%{opacity:.9}100%{transform:translateY(-80vh) rotate(225deg);opacity:0}}`}</style>
    </div>
  );
}
