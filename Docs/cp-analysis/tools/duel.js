// duel.js — does higher CP always win the fight?
// Simulates 1v1 player-vs-enemy using the project's REAL combat model, and compares the
// outcome against the CP each side would display. Analysis only; changes nothing.
//
// Combat model, verified in source:
//   CombatMath.cs:15-18   dmgPerHit = max(1, attack * 100/(100+defenderDefense))
//   PlayerAttackState.cs:100 + PlayerManager.cs:798-812   player cadence = recoveryTime = 0.6s
//   EnemyManager.cs:579/708                                enemy  cadence = recoveryTime = 0.6s
//   Both sides therefore share a 0.6s cadence, so hits-to-kill decides the duel.

const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS, 'cpcalc.js')}"`, { stdio: 'pipe' });
const D = JSON.parse(fs.readFileSync(path.join(TOOLS, 'out.json'), 'utf8'));

const CADENCE = 0.6;
const dmg = (atk, defenderDef) => Math.max(1, atk * 100 / (100 + Math.max(0, defenderDef)));
const hitsToKill = (hp, perHit) => Math.ceil(hp / perHit);

// ---- build a player's COMBAT stats (PlayerStatsApplier applies all six multipliers) ----
function playerCombat(base, L) {
  const g = D.growthPlayer[L - 1];
  return { atk: base.attack * g.gA, hp: base.maxHP * g.gH, def: base.defense * g.gD, L };
}
// ---- what the units screen DISPLAYS (UnitsPanelController applies only gA/gH/gMv/gAS) ----
function playerDisplayedCP(base, L) {
  const g = D.growthPlayer[L - 1], w = D.weightsPlayer[L - 1];
  return Math.round(
    w.wA * base.attack * g.gA + w.wH * base.maxHP * g.gH + w.wMv * base.moveSpeed * g.gMv +
    w.wAS * base.attackSpeed * g.gAS + w.wD * base.defense + w.wR * base.attackRange);
}
// ---- enemy: RebuildFromBase applies only gA/gH/gMv/gAS, so defense never grows ----
function enemyCombat(base, S) {
  const g = D.growthEnemy[S - 1];
  return { atk: base.attack * g.gA, hp: base.maxHP * g.gH, def: base.defense, S };
}
function enemyCP(base, S) {
  const g = D.growthEnemy[S - 1], w = D.weightsEnemy[S - 1];
  return Math.round(
    w.wA * base.attack * g.gA + w.wH * base.maxHP * g.gH + w.wMv * base.moveSpeed * g.gMv +
    w.wAS * base.attackSpeed * g.gAS + w.wD * base.defense + w.wR * 0.83);
}

const profiles = [
  { name: 'fast (ATK 64/AS 2.0)', base: D.heroes.find(h => h.id === 1).base },
  { name: 'slow (ATK 72/AS 1.5)', base: D.heroes.find(h => h.id === 2).base },
];

let total = 0, mispredicted = 0;
const examples = [];

for (const p of profiles) {
  for (const L of [1, 3, 5, 10, 15, 20]) {
    const pc = playerCombat(p.base, L), pcp = playerDisplayedCP(p.base, L);
    for (const e of D.enemies) {
      for (const S of [1, 3, 5, 10, 15, 20]) {
        const ec = enemyCombat(e.base, S), ecp = enemyCP(e.base, S);

        const pHits = hitsToKill(ec.hp, dmg(pc.atk, ec.def));   // player kills enemy in this many
        const eHits = hitsToKill(pc.hp, dmg(ec.atk, pc.def));   // enemy kills player in this many

        let winner;
        if (pHits < eHits) winner = 'player';
        else if (eHits < pHits) winner = 'enemy';
        else winner = 'mutual';
        if (winner === 'mutual') continue;                       // ignore ties

        const cpFavours = pcp > ecp ? 'player' : (ecp > pcp ? 'enemy' : 'tie');
        if (cpFavours === 'tie') continue;

        total++;
        if (cpFavours !== winner) {
          mispredicted++;
          examples.push({
            p: p.name, L, pcp, e: e.name.replace('Enemy_', ''), S, ecp,
            pHits, eHits, winner, cpFavours,
            pAtk: pc.atk, pDef: pc.def, pHp: pc.hp,
            eAtk: ec.atk, eDef: ec.def, eHp: ec.hp,
            cpGap: Math.abs(pcp - ecp),
          });
        }
      }
    }
  }
}

console.log('================ DOES HIGHER CP ALWAYS WIN? ================\n');
console.log(`matchups evaluated (excluding ties): ${total}`);
console.log(`CP predicted the WRONG winner      : ${mispredicted}  (${(100*mispredicted/total).toFixed(1)}%)\n`);

console.log('--- the clearest counterexamples (lower CP wins) ---\n');
examples.sort((a, b) => b.cpGap - a.cpGap);
for (const x of examples.slice(0, 10)) {
  const pw = x.winner === 'player';
  console.log(`${x.p} @L${x.L}  vs  ${x.e} @S${x.S}`);
  console.log(`   player: CP ${x.pcp.toString().padStart(4)}  ATK ${x.pAtk.toFixed(0).padStart(4)}  DEF ${x.pDef.toFixed(0).padStart(3)}  HP ${x.pHp.toFixed(0).padStart(5)}   kills in ${x.pHits} hits (${(x.pHits*CADENCE).toFixed(1)}s)`);
  console.log(`   enemy : CP ${x.ecp.toString().padStart(4)}  ATK ${x.eAtk.toFixed(0).padStart(4)}  DEF ${x.eDef.toFixed(0).padStart(3)}  HP ${x.eHp.toFixed(0).padStart(5)}   kills in ${x.eHits} hits (${(x.eHits*CADENCE).toFixed(1)}s)`);
  console.log(`   CP says ${x.cpFavours.toUpperCase()} (by ${x.cpGap})  ->  ACTUAL WINNER: ${x.winner.toUpperCase()}  ** CP WRONG **\n`);
}

// ---------- why: how much does each stat really matter in a duel? ----------
console.log('--- sensitivity: +10% to one stat, effect on hits-to-kill in a duel ---\n');
const P = playerCombat(profiles[0].base, 10);
const E = enemyCombat(D.enemies.find(e => e.name === 'Enemy_Skeleton_Crusader_1').base, 10);
const baseP = hitsToKill(E.hp, dmg(P.atk, E.def));
const baseE = hitsToKill(P.hp, dmg(E.atk, P.def));
console.log(`baseline: player needs ${baseP} hits, enemy needs ${baseE} hits`);
for (const [label, mod] of [
  ['player ATK +10%', p => ({ ...p, atk: p.atk * 1.1 })],
  ['player HP  +10%', p => ({ ...p, hp: p.hp * 1.1 })],
  ['player DEF +10%', p => ({ ...p, def: p.def * 1.1 })],
]) {
  const P2 = mod(P);
  const a = hitsToKill(E.hp, dmg(P2.atk, E.def));
  const b = hitsToKill(P2.hp, dmg(E.atk, P2.def));
  console.log(`  ${label}:  player needs ${a} hits (was ${baseP}), enemy needs ${b} hits (was ${baseE})`);
}
console.log('\nCP weight of each of those at L10:  ATK 0.982   HP 0.144   DEF 0.009');
