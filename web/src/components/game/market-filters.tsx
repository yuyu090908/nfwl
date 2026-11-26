import { CRYSTAL_TYPES, POSITIONS, RARITIES } from "@/lib/domain/rules";
import { Button } from "@/components/ui/button";

const sel = "h-10 rounded-lg border border-line bg-bg-2 px-2 text-sm";

/** Plain GET form: works without JavaScript, keeps filters in the URL (shareable, back button). */
export function MarketFilters({ sp, skills, action }: { sp: Record<string, string | undefined>; skills: { id: string; name: string }[]; action: string }) {
  return (
    <form method="get" action={action} className="grid grid-cols-2 gap-2 rounded-2xl border border-line bg-card/60 p-4 sm:grid-cols-4 lg:grid-cols-8" aria-label="絞り込み">
      <label className="sr-only" htmlFor="f-r">Rarity</label>
      <select id="f-r" name="rarity" defaultValue={sp.rarity ?? ""} className={sel}><option value="">Rarity</option>{RARITIES.map((r) => <option key={r}>{r}</option>)}</select>
      <label className="sr-only" htmlFor="f-p">Position</label>
      <select id="f-p" name="position" defaultValue={sp.position ?? ""} className={sel}><option value="">Position</option>{POSITIONS.map((r) => <option key={r}>{r}</option>)}</select>
      <label className="sr-only" htmlFor="f-c">Crystal</label>
      <select id="f-c" name="crystal" defaultValue={sp.crystal ?? ""} className={sel}><option value="">Crystal type</option>{CRYSTAL_TYPES.map((r) => <option key={r}>{r}</option>)}</select>
      <label className="sr-only" htmlFor="f-s">Skill</label>
      <select id="f-s" name="skill" defaultValue={sp.skill ?? ""} className={sel}><option value="">Skill</option>{skills.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
      <input name="minOvr" type="number" min={40} max={99} placeholder="OVR min" defaultValue={sp.minOvr} className={sel} aria-label="OVR 最小" />
      <input name="minLevel" type="number" min={1} max={60} placeholder="Level min" defaultValue={sp.minLevel} className={sel} aria-label="レベル 最小" />
      <input name="maxPrice" type="number" min={0} placeholder="Price max" defaultValue={sp.maxPrice} className={sel} aria-label="価格 上限" />
      <select name="sort" defaultValue={sp.sort ?? "recent"} className={sel} aria-label="並び替え">
        <option value="price_asc">Lowest price</option>
        <option value="price_desc">Highest price</option>
        <option value="ovr">Highest OVR</option>
        <option value="recent">Recently listed</option>
        <option value="sold">Recently sold</option>
      </select>
      <Button type="submit" size="sm" variant="crystal" className="col-span-2 sm:col-span-4 lg:col-span-8">Apply filters</Button>
    </form>
  );
}
