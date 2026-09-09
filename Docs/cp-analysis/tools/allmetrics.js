// allmetrics.js — evaluate ALL CP metrics (range excluded per directive).
// Two scenarios: (A) the game as it is today, (B) if attackSpeed were made functional.
const { execSync } = require('child_process');
const fs = require('fs'), path = require('path');
const TOOLS = 'F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(TOOLS, 'cpcalc.js')}"`, { stdio: 'pipe' });
const D = JSON.parse(fs.readFileSync(path.join(TOOLS, 'out.json'), 'utf8'));

const dmg = (a,d) => Math.max(1, a*100/(100+Math.max(0,d)));
const ehp = s => s.hp * (100 + Math.max(0,s.def)) / 100;

function pStats(b,L){const g=D.growthPlayer[L-1];return{
  atk:b.attack*g.gA, hp:b.maxHP*g.gH, def:b.defense*g.gD,
  as:b.attackSpeed*g.gAS, ms:b.moveSpeed*g.gMv};}
function eStats(b,S){const g=D.growthEnemy[S-1];return{
  atk:b.attack*g.gA, hp:b.maxHP*g.gH, def:b.defense,
  as:b.attackSpeed*g.gAS, ms:b.moveSpeed*g.gMv};}

const heroes=[D.heroes.find(h=>h.id===1).base, D.heroes.find(h=>h.id===2).base];
const LV=[1,3,5,10,15,20], SG=[1,3,5,10,15,20];

// ---- build matchups under a chosen cadence rule ----
function buildMatchups(atkSpeedWorks){
  const M=[];
  for(const b of heroes) for(const L of LV){
    const P=pStats(b,L);
    for(const e of D.enemies) for(const S of SG){
      const E=eStats(e.base,S);
      // cadence: today both are a flat 0.6s. If attackSpeed worked, cadence = 0.6 / attackSpeed.
      const pCad = atkSpeedWorks ? 0.6/P.as : 0.6;
      const eCad = atkSpeedWorks ? 0.6/E.as : 0.6;
      const pTime = Math.ceil(E.hp/dmg(P.atk,E.def)) * pCad;   // time for player to kill
      const eTime = Math.ceil(P.hp/dmg(E.atk,P.def)) * eCad;
      if (Math.abs(pTime-eTime) < 1e-9) continue;
      M.push({P,E,win: pTime<eTime ? 'p':'e'});
    }
  }
  return M;
}

function score(M, f){
  let ok=0,n=0;
  for(const m of M){const a=f(m.P),b=f(m.E); if(a===b)continue; n++; if((a>b?'p':'e')===m.win)ok++;}
  return 100*ok/n;
}

function report(title, M, atkSpeedWorks){
  console.log(`\n${'='.repeat(64)}\n${title}   (${M.length} duels)\n${'='.repeat(64)}`);
  const cands = [
    ['current shipped CP (wA1 wH.15 wD0 wAS.4 wMv.25)',
      s => 1*s.atk + 0.15*s.hp + 0*s.def + 0.40*s.as + 0.25*s.ms],
    ['ATK x EffectiveHP                            ', s => s.atk*ehp(s)],
    ['ATK x EffectiveHP x AtkSpd                   ', s => s.atk*ehp(s)*s.as],
    ['ATK x EffectiveHP x MoveSpd                  ', s => s.atk*ehp(s)*s.ms],
    ['ATK x EffectiveHP x AtkSpd x MoveSpd         ', s => s.atk*ehp(s)*s.as*s.ms],
  ];
  for(const [name,f] of cands) console.log(`  ${name}  ${score(M,f).toFixed(1).padStart(6)}%`);
}

const Mnow = buildMatchups(false);
report('SCENARIO A - the game AS IT IS (attackSpeed inert, cadence flat 0.6s)', Mnow, false);
const Mfix = buildMatchups(true);
report('SCENARIO B - IF attackSpeed were made functional (cadence = 0.6/atkSpd)', Mfix, true);

// ---- does adding an inert stat HURT? ----
console.log(`\n${'='.repeat(64)}\nDOES INCLUDING AN INERT STAT DAMAGE THE METRIC?\n${'='.repeat(64)}`);
console.log('  Scenario A (attackSpeed and moveSpeed do nothing in combat):');
console.log(`    ATK x EHP                       ${score(Mnow, s=>s.atk*ehp(s)).toFixed(1)}%   <- correct`);
console.log(`    ATK x EHP x AtkSpd              ${score(Mnow, s=>s.atk*ehp(s)*s.as).toFixed(1)}%   <- adding an inert stat`);
console.log(`    ATK x EHP x MoveSpd             ${score(Mnow, s=>s.atk*ehp(s)*s.ms).toFixed(1)}%   <- adding an inert stat`);

// ---- optimal linear weights, range excluded, 5 stats ----
console.log(`\n${'='.repeat(64)}\nBEST WEIGHTED SUM (range removed), scenario A\n${'='.repeat(64)}`);
let best={acc:-1};
for(let wH=0.05;wH<=1.5001;wH+=0.05)
 for(let wD=0;wD<=3.0001;wD+=0.1)
  for(const wAS of [0,0.4,2,10])
   for(const wMv of [0,0.25,2]){
     const f=s=>1*s.atk+wH*s.hp+wD*s.def+wAS*s.as+wMv*s.ms;
     const a=score(Mnow,f);
     if(a>best.acc+1e-9) best={acc:a,wH:+wH.toFixed(2),wD:+wD.toFixed(1),wAS,wMv};
   }
console.log(`  wA 1.00  wH ${best.wH}  wD ${best.wD}  wAS ${best.wAS}  wMv ${best.wMv}  ->  ${best.acc.toFixed(1)}%`);

// share of CP under that best sum
const P10=pStats(heroes[0],10);
const t={atk:1*P10.atk, hp:best.wH*P10.hp, def:best.wD*P10.def, as:best.wAS*P10.as, ms:best.wMv*P10.ms};
const tot=Object.values(t).reduce((a,b)=>a+b,0);
console.log('\n  resulting share of CP, "fast" hero at level 10:');
for(const k of ['atk','hp','def','as','ms'])
  console.log(`     ${k.padEnd(4)} ${(100*t[k]/tot).toFixed(1).padStart(5)}%`);
