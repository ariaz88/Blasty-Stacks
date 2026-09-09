// share.js — can ATK be ~45-50% and AtkSpd ~10% while CP still predicts duels?
// Distinguishes ELASTICITY (fixed by the combat math) from PRACTICAL INFLUENCE
// (set by how wide a range you author each stat over — which IS tunable).

const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS,'cpcalc.js')}"`, {stdio:'pipe'});
const D = JSON.parse(fs.readFileSync(path.join(TOOLS,'out.json'),'utf8'));

// CP = ATK * AtkSpd * HP * (1 + DEF/100)
// log CP = log ATK + log AtkSpd + log HP + log(1+DEF/100)
// A stat's PRACTICAL influence over the roster = how much its log term VARIES.

function ranges(label, atkR, asR, hpR, defR) {
  const span = ([lo,hi]) => Math.log(hi/lo);
  const t = { ATK: span(atkR), AtkSpd: span(asR), HP: span(hpR),
              DEF: Math.log((1+defR[1]/100)/(1+defR[0]/100)) };
  const tot = Object.values(t).reduce((a,b)=>a+b,0);
  console.log(`\n  ${label}`);
  console.log(`    ATK    ${atkR[0]}-${atkR[1]}`.padEnd(26) + `influence ${(100*t.ATK/tot).toFixed(1).padStart(5)}%`);
  console.log(`    AtkSpd ${asR[0]}-${asR[1]}`.padEnd(26)  + `influence ${(100*t.AtkSpd/tot).toFixed(1).padStart(5)}%`);
  console.log(`    HP     ${hpR[0]}-${hpR[1]}`.padEnd(26)  + `influence ${(100*t.HP/tot).toFixed(1).padStart(5)}%`);
  console.log(`    DEF    ${defR[0]}-${defR[1]}`.padEnd(26) + `influence ${(100*t.DEF/tot).toFixed(1).padStart(5)}%`);
  return t;
}

console.log('=========== PRACTICAL INFLUENCE = HOW WIDE YOU AUTHOR EACH STAT ===========');
console.log('  (elasticity is fixed at ATK=AtkSpd=HP=1 by the combat math; what you CAN');
console.log('   control is the RANGE each stat spans across the roster + 20 levels)');

// current actual spans across heroes+enemies and levels 1..20
ranges('CURRENT authored ranges (heroes + enemies, L1-20):',
       [32, 252], [0.85, 2.0], [100, 1680], [25, 78]);

console.log('\n  -> AtkSpd already sits near 10%, but ATK is dragged down by HP\'s huge span.');
console.log('     HP spans 16.8x because HP growth (+10%/lvl) outruns ATK growth (+8%/lvl).');

ranges('IF AtkSpd is narrowed to 0.9-1.3 (keeps balance, cadence stays ~0.6s):',
       [32, 252], [0.9, 1.3], [100, 1680], [25, 78]);

ranges('IF ALSO HP growth is brought closer to ATK growth (HP span 100-900):',
       [32, 252], [0.9, 1.3], [100, 900], [25, 78]);

ranges('TARGET-SEEKING: ATK ~47%, AtkSpd ~10%, HP ~35%, DEF ~8%',
       [32, 300], [0.9, 1.35], [100, 700], [25, 110]);

// --- what cadence do candidate AtkSpd ranges give? ---
console.log('\n=========== CADENCE CHECK (recoveryTime 0.6 / attackSpeed) ===========');
for (const as of [0.85, 0.9, 1.0, 1.15, 1.3, 1.5, 2.0]) {
  const cad = 0.6/as;
  console.log(`   attackSpeed ${as.toFixed(2)}  ->  ${cad.toFixed(3)}s per hit   (${(1/cad).toFixed(2)} hits/sec)`);
}
console.log('\n   today every unit is a flat 0.600s (1.67 hits/sec) regardless of the stat.');
console.log('   Keeping AtkSpd in 0.9-1.3 keeps cadence in 0.46-0.67s: close to today,');
console.log('   so the fix does NOT hand the player a 2x damage jump.');
