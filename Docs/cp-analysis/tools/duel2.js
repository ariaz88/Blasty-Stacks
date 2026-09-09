// duel2.js — which power metric actually predicts who wins the fight?
// Same 407 real matchups; scores each side with four candidate metrics and counts how often
// each predicts the true winner. Analysis only.

const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS, 'cpcalc.js')}"`, { stdio: 'pipe' });
const D = JSON.parse(fs.readFileSync(path.join(TOOLS, 'out.json'), 'utf8'));

const dmg = (atk, def) => Math.max(1, atk * 100 / (100 + Math.max(0, def)));
const htk = (hp, per) => Math.ceil(hp / per);

function playerCombat(b, L) { const g = D.growthPlayer[L-1];
  return { atk: b.attack*g.gA, hp: b.maxHP*g.gH, def: b.defense*g.gD,
           ms: b.moveSpeed*g.gMv, as: b.attackSpeed*g.gAS, rng: b.attackRange*g.gR }; }
function enemyCombat(b, S) { const g = D.growthEnemy[S-1];
  return { atk: b.attack*g.gA, hp: b.maxHP*g.gH, def: b.defense,
           ms: b.moveSpeed*g.gMv, as: b.attackSpeed*g.gAS, rng: 0.83 }; }

// --- the four metrics ---
// A: the current shipped CP (weighted sum, weights sampled at the unit's level)
function A(s, w) { return w.wA*s.atk + w.wH*s.hp + w.wMv*s.ms + w.wAS*s.as + w.wD*s.def + w.wR*s.rng; }
// A': current CP with the three inert stats removed (move / atkSpd / range)
function Aprime(s, w) { return w.wA*s.atk + w.wH*s.hp + w.wD*s.def; }
// B: Pokemon-GO shape, product with sqrt damping
function B(s) { return s.atk * Math.sqrt(Math.max(1e-6, s.def)) * Math.sqrt(Math.max(1e-6, s.hp)); }
// C: damage output x effective HP  (EffectiveHP = HP * (100+DEF)/100, per CPCalculator.EffectiveHP)
function C(s) { return s.atk * (s.hp * (100 + Math.max(0, s.def)) / 100); }

const profiles = [
  { name: 'fast', base: D.heroes.find(h => h.id === 1).base },
  { name: 'slow', base: D.heroes.find(h => h.id === 2).base },
];

const metrics = { 'A  (current CP)': null, "A' (CP minus inert stats)": null,
                  'B  (product, sqrt-damped)': null, 'C  (ATK x EffectiveHP)': null };
const score = { A: 0, Ap: 0, B: 0, C: 0 };
let total = 0;

for (const p of profiles) {
  for (const L of [1,3,5,10,15,20]) {
    const pc = playerCombat(p.base, L), wp = D.weightsPlayer[L-1];
    for (const e of D.enemies) {
      for (const S of [1,3,5,10,15,20]) {
        const ec = enemyCombat(e.base, S), we = D.weightsEnemy[S-1];
        const pH = htk(ec.hp, dmg(pc.atk, ec.def));
        const eH = htk(pc.hp, dmg(ec.atk, pc.def));
        if (pH === eH) continue;
        const winner = pH < eH ? 'p' : 'e';
        total++;
        const cmp = (fp, fe) => (fp > fe ? 'p' : fp < fe ? 'e' : null);
        const rA  = cmp(A(pc, wp),      A(ec, we));
        const rAp = cmp(Aprime(pc, wp), Aprime(ec, we));
        const rB  = cmp(B(pc),          B(ec));
        const rC  = cmp(C(pc),          C(ec));
        if (rA  === winner) score.A++;
        if (rAp === winner) score.Ap++;
        if (rB  === winner) score.B++;
        if (rC  === winner) score.C++;
      }
    }
  }
}

console.log('======= WHICH METRIC PREDICTS THE ACTUAL DUEL WINNER? =======\n');
console.log(`matchups evaluated (ties excluded): ${total}\n`);
const rows = [
  ['A   current shipped CP',            score.A],
  ["A'  CP minus the 3 inert stats",    score.Ap],
  ['B   product with sqrt damping',     score.B],
  ['C   ATK x EffectiveHP',             score.C],
];
rows.sort((x, y) => y[1] - x[1]);
for (const [name, s] of rows) {
  const pct = 100 * s / total;
  const bar = '#'.repeat(Math.round(pct / 2));
  console.log(`  ${name.padEnd(34)} ${String(s).padStart(3)}/${total}  ${pct.toFixed(1).padStart(5)}%  ${bar}`);
}
console.log('\n(50% would be a coin flip. Higher is better.)');

// direction of A's errors — is the bias systematic?
let apOver = 0, apUnder = 0;
for (const p of profiles) {
  for (const L of [1,3,5,10,15,20]) {
    const pc = playerCombat(p.base, L), wp = D.weightsPlayer[L-1];
    for (const e of D.enemies) {
      for (const S of [1,3,5,10,15,20]) {
        const ec = enemyCombat(e.base, S), we = D.weightsEnemy[S-1];
        const pH = htk(ec.hp, dmg(pc.atk, ec.def)), eH = htk(pc.hp, dmg(ec.atk, pc.def));
        if (pH === eH) continue;
        const winner = pH < eH ? 'p' : 'e';
        const rA = A(pc, wp) > A(ec, we) ? 'p' : 'e';
        if (rA !== winner) { if (rA === 'p') apOver++; else apUnder++; }
      }
    }
  }
}
console.log(`\n--- direction of the current CP's mistakes ---`);
console.log(`  CP said PLAYER wins, enemy actually won : ${apOver}`);
console.log(`  CP said ENEMY wins, player actually won : ${apUnder}`);
console.log(apOver > 0 && apUnder === 0
  ? '  => the bias is entirely one-directional: CP systematically OVERRATES the player.'
  : '  => errors go both ways.');
