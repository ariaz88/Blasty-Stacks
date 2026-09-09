// weights.js — if CP stays a weighted SUM, what weights predict duels best?
// Grid-searches wA/wH/wD against the 408 real matchups, and compares to the product form.
// Analysis only.

const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS, 'cpcalc.js')}"`, { stdio: 'pipe' });
const D = JSON.parse(fs.readFileSync(path.join(TOOLS, 'out.json'), 'utf8'));

const dmg = (a, d) => Math.max(1, a * 100 / (100 + Math.max(0, d)));
const htk = (hp, per) => Math.ceil(hp / per);

function pc(b, L) { const g = D.growthPlayer[L-1];
  return { atk: b.attack*g.gA, hp: b.maxHP*g.gH, def: b.defense*g.gD }; }
function ec(b, S) { const g = D.growthEnemy[S-1];
  return { atk: b.attack*g.gA, hp: b.maxHP*g.gH, def: b.defense }; }

// build the matchup set once
const M = [];
for (const p of [D.heroes.find(h=>h.id===1).base, D.heroes.find(h=>h.id===2).base])
  for (const L of [1,3,5,10,15,20]) {
    const P = pc(p, L);
    for (const e of D.enemies) for (const S of [1,3,5,10,15,20]) {
      const E = ec(e.base, S);
      const pH = htk(E.hp, dmg(P.atk, E.def)), eH = htk(P.hp, dmg(E.atk, P.def));
      if (pH === eH) continue;
      M.push({ P, E, win: pH < eH ? 'p' : 'e' });
    }
  }

function accSum(wA, wH, wD) {
  let ok = 0;
  for (const m of M) {
    const sp = wA*m.P.atk + wH*m.P.hp + wD*m.P.def;
    const se = wA*m.E.atk + wH*m.E.hp + wD*m.E.def;
    if (sp === se) continue;
    if ((sp > se ? 'p' : 'e') === m.win) ok++;
  }
  return ok / M.length;
}
const ehp = s => s.hp * (100 + Math.max(0, s.def)) / 100;
function accProd() {
  let ok = 0;
  for (const m of M) {
    const sp = m.P.atk * ehp(m.P), se = m.E.atk * ehp(m.E);
    if ((sp > se ? 'p' : 'e') === m.win) ok++;
  }
  return ok / M.length;
}

console.log(`matchups: ${M.length}\n`);
console.log(`current shipped weights (wA 1.00, wH 0.15, wD 0.00) : ${(100*accSum(1,0.15,0)).toFixed(1)}%`);
console.log(`product  ATK x EffectiveHP                          : ${(100*accProd()).toFixed(1)}%\n`);

// ---- grid search the best SUM ----
let best = { acc: -1 };
for (let wH = 0.05; wH <= 3.0001; wH += 0.05)
  for (let wD = 0; wD <= 6.0001; wD += 0.05) {
    const a = accSum(1, wH, wD);
    if (a > best.acc + 1e-12) best = { acc: a, wH: +wH.toFixed(2), wD: +wD.toFixed(2) };
  }
console.log(`BEST possible weighted SUM: wA 1.00, wH ${best.wH}, wD ${best.wD}  ->  ${(100*best.acc).toFixed(1)}%`);
console.log(`   (ceiling for the current formula SHAPE, no matter how it is tuned)\n`);

// ---- what SHARE of CP does each stat then take, for a real hero? ----
function shares(wA, wH, wD, s, label) {
  const tA = wA*s.atk, tH = wH*s.hp, tD = wD*s.def, t = tA+tH+tD;
  console.log(`   ${label.padEnd(22)} ATK ${(100*tA/t).toFixed(1).padStart(5)}%   HP ${(100*tH/t).toFixed(1).padStart(5)}%   DEF ${(100*tD/t).toFixed(1).padStart(5)}%`);
}
console.log('--- share of CP per stat, hero "fast" profile ---');
for (const L of [1, 10, 20]) {
  const P = pc(D.heroes.find(h=>h.id===1).base, L);
  console.log(`  level ${L}:`);
  shares(1, 0.15, 0, P, 'current shipped');
  shares(1, best.wH, best.wD, P, 'best possible sum');
}

// ---- elasticity of the product form: the theoretically correct "share" ----
console.log('\n--- product form: proportional importance (elasticity) ---');
console.log('  power = ATK x HP x (1 + DEF/100).  A 1% rise in each stat raises power by:');
for (const [lbl, def] of [['player DEF 25', 25], ['player DEF 43 (L10 combat)', 43.15],
                          ['Skeleton DEF 52', 52], ['Golem_02 DEF 78', 78]]) {
  const eA = 1, eH = 1, eD = (def/100) / (1 + def/100);
  const t = eA + eH + eD;
  console.log(`   ${lbl.padEnd(28)} ATK ${(100*eA/t).toFixed(1).padStart(5)}%   HP ${(100*eH/t).toFixed(1).padStart(5)}%   DEF ${(100*eD/t).toFixed(1).padStart(5)}%`);
}
