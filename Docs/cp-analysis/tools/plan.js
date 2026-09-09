// plan.js — (1) shares under the new product CP, (2) the upgrade-cadence vs enemy-growth problem.
const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS,'cpcalc.js')}"`, {stdio:'pipe'});
const D = JSON.parse(fs.readFileSync(path.join(TOOLS,'out.json'),'utf8'));

console.log('============ 1) SHARE OF CP UNDER  (ATK x AtkSpd) x EffectiveHP ============\n');
console.log('For a product, "share" = elasticity: how much power a +1% change in that stat buys.\n');
for (const [lbl, def] of [['hero DEF 25 (L1)',25], ['hero DEF 43 (L10)',43.15], ['hero DEF 58 (L15)',58]]) {
  const e = { ATK:1, AtkSpd:1, HP:1, DEF:(def/100)/(1+def/100) };
  const t = Object.values(e).reduce((a,b)=>a+b,0);
  const p = k => (100*e[k]/t).toFixed(1).padStart(5);
  console.log(`  ${lbl.padEnd(20)} ATK ${p('ATK')}%  AtkSpd ${p('AtkSpd')}%  HP ${p('HP')}%  DEF ${p('DEF')}%`);
  console.log(`  ${''.padEnd(20)} -> offense (ATK x AtkSpd) combined = ${(100*(e.ATK+e.AtkSpd)/t).toFixed(1)}%\n`);
}
console.log('  Compare: current shipped CP gives ATK alone 79% at L1, 76% at L20.\n');

// ============ 2) the asymmetry ============
console.log('============ 2) PLAYER UPGRADES EVERY ~4.5 STAGES vs ENEMY LEVEL = STAGE ============\n');
console.log('  stage | enemy lvl | player lvl | enemy power | player power | ratio (enemy/player)');
console.log('  ------+-----------+------------+-------------+--------------+---------------------');

const hero = D.heroes.find(h=>h.id===1).base;          // ATK 64 / AS 2.0 / HP 100 / DEF 25
const golem = D.enemies.find(e=>e.name==='Enemy_Golem_02').base;
const ehp = (hp,def) => hp*(100+def)/100;

for (const S of [1,4,8,12,16,20]) {
  const pl = Math.max(1, Math.floor((S-1)/4.5)+1);      // an upgrade roughly every 4.5 stages
  const gp = D.growthPlayer[pl-1], ge = D.growthEnemy[S-1];
  // player, with the CURRENT curves
  const pAtk = hero.attack*gp.gA, pHp = hero.maxHP*gp.gH, pDef = hero.defense*gp.gD;
  const pPow = pAtk * ehp(pHp,pDef);
  // enemy (defense does not grow today)
  const eAtk = golem.attack*ge.gA, eHp = golem.maxHP*ge.gH, eDef = golem.defense;
  const ePow = eAtk * ehp(eHp,eDef);
  console.log(`   ${String(S).padStart(3)}  |    ${String(S).padStart(2)}     |     ${String(pl).padStart(2)}     | ${ePow.toExponential(2)}    | ${pPow.toExponential(2)}     |  ${(ePow/pPow).toFixed(1)}x`);
}

console.log('\n  A ratio of 1.0 means an even 1v1. Higher = the enemy is stronger.\n');

// ============ 3) what growth rate would keep it sane? ============
console.log('============ 3) WHAT PLAYER GROWTH RATE KEEPS PACE? ============\n');
console.log('  Enemy power multiplier from stage 1 -> 20, with the current enemy curves:');
const ge1 = D.growthEnemy[0], ge20 = D.growthEnemy[19];
const eP1  = golem.attack*ge1.gA  * ehp(golem.maxHP*ge1.gH,  golem.defense);
const eP20 = golem.attack*ge20.gA * ehp(golem.maxHP*ge20.gH, golem.defense);
console.log(`     enemy power x${(eP20/eP1).toFixed(1)} over 20 stages\n`);

console.log('  If the player gets only ~5 upgrades in that span, each upgrade must deliver:');
for (const ups of [4,5]) {
  const need = Math.pow(eP20/eP1, 1/ups);
  console.log(`     ${ups} upgrades -> x${need.toFixed(2)} power per upgrade  (to fully keep pace)`);
}
console.log('\n  Power here is ATK x HP x (1+DEF/100), so a per-upgrade power factor F needs');
console.log('  roughly F^(1/3) on each of ATK, HP and DEF if spread evenly:');
for (const ups of [4,5]) {
  const need = Math.pow(eP20/eP1, 1/ups);
  const per = Math.pow(need, 1/3);
  console.log(`     ${ups} upgrades -> +${((per-1)*100).toFixed(0)}% on each stat per upgrade`);
}
console.log('\n  With the CURRENT curves, one upgrade (one level) gives only about:');
const g1=D.growthPlayer[0], g2=D.growthPlayer[1];
console.log(`     ATK +${((g2.gA/g1.gA-1)*100).toFixed(1)}%   HP +${((g2.gH/g1.gH-1)*100).toFixed(1)}%   DEF +${((g2.gD/g1.gD-1)*100).toFixed(1)}%`);
