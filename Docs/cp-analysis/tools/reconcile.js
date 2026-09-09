// reconcile.js — why did one test say 100% and another say 52.9%?
// Hypothesis: with EQUAL cadence, ceil() can only cause DRAWS, never reversals.
// With attackSpeed driving cadence, ceil() CAN reverse the order.

const R = 0.6;
const ehp = u => u.hp * (100 + Math.max(0,u.def)) / 100;
const hits = (A,B) => Math.ceil(ehp(B)/A.atk);

// MODEL 1: today's game - both sides flat 0.6s cadence. CP = ATK x EHP
function model1(A,B){
  const ha=hits(A,B), hb=hits(B,A);
  if(ha===hb) return 'draw';
  return ha<hb ? 'a':'b';
}
const CP1 = u => u.atk * ehp(u);

// MODEL 2: proposed - cadence = R/attackSpeed. CP = ATK x AtkSpd x EHP
function model2(A,B){
  const ta=hits(A,B)*(R/A.as), tb=hits(B,A)*(R/B.as);
  if(Math.abs(ta-tb)<1e-12) return 'draw';
  return ta<tb ? 'a':'b';
}
const CP2 = u => u.atk * u.as * ehp(u);

function test(model, CP, label, varyAS){
  const rnd=(a,b)=>a+Math.random()*(b-a);
  let n=0, draw=0, correct=0, REVERSED=0;
  let worstRev=0;
  for(let i=0;i<500000;i++){
    const A={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:varyAS?rnd(0.9,1.3):1};
    const B={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:varyAS?rnd(0.9,1.3):1};
    const ca=CP(A), cb=CP(B); if(ca===cb) continue;
    const r=model(A,B);
    n++;
    if(r==='draw'){draw++;continue;}
    const says = ca>cb?'a':'b';
    if(r===says) correct++;
    else { REVERSED++; const m=100*Math.abs(ca-cb)/Math.min(ca,cb); if(m>worstRev)worstRev=m; }
  }
  const decided = n-draw;
  console.log(`\n${label}`);
  console.log(`  pairs                 ${n.toLocaleString()}`);
  console.log(`  draws (no winner)     ${draw.toLocaleString()}  (${(100*draw/n).toFixed(2)}%)`);
  console.log(`  decided duels         ${decided.toLocaleString()}`);
  console.log(`  higher CP WON         ${correct.toLocaleString()}  (${(100*correct/decided).toFixed(3)}% of decided)`);
  console.log(`  higher CP LOST        ${REVERSED.toLocaleString()}  <-- REVERSALS`);
  if(REVERSED) console.log(`  largest reversal margin ${worstRev.toFixed(1)}%`);
  return {draw:100*draw/n, acc:100*correct/decided, rev:REVERSED};
}

console.log('================================================================');
console.log(' RECONCILING THE TWO RESULTS');
console.log('================================================================');

const m1 = test(model1, CP1, 'MODEL 1 - today: flat 0.6s cadence, CP = ATK x EffectiveHP', false);
const m2 = test(model2, CP2, 'MODEL 2 - proposed: cadence = 0.6/AtkSpd, CP = ATK x AtkSpd x EHP', true);

console.log('\n================================================================');
console.log(' THE ANSWER');
console.log('================================================================');
console.log(`
 MODEL 1 reversals: ${m1.rev}    MODEL 2 reversals: ${m2.rev}

 With EQUAL cadence the order can never invert. If x < y then ceil(x) <= ceil(y),
 so the higher-CP unit can only ever TIE, never lose. Errors show up purely as
 draws (${m1.draw.toFixed(1)}% of pairs).

 Once attackSpeed drives cadence, the comparison becomes
 ceil(a)*(R/asA) vs ceil(b)*(R/asB). Multiplying two rounded values by DIFFERENT
 cadences CAN flip the order - which is where the ${m2.rev.toLocaleString()} reversals come from.

 => Enabling attackSpeed is what introduces the reversals.
    It is a real trade-off, not a flaw in the formula.
`);

// what fraction of the 408 real matchups sit at small CP margins?
console.log('================================================================');
console.log(' WHY THE 408 REAL MATCHUPS SCORED 100%');
console.log('================================================================');
const { execSync } = require('child_process');
const fs=require('fs'), path=require('path');
const T='F:\\Projects\\unitypProjects\\Stacky  Warriors  2D\\Blasty-Stacks\\Docs\\cp-analysis\\tools';
execSync(`node "${path.join(T,'cpcalc.js')}"`,{stdio:'pipe'});
const D=JSON.parse(fs.readFileSync(path.join(T,'out.json'),'utf8'));
const pc=(b,L)=>{const g=D.growthPlayer[L-1];return{atk:b.attack*g.gA,hp:b.maxHP*g.gH,def:b.defense*g.gD,as:1};};
const ec=(b,S)=>{const g=D.growthEnemy[S-1];return{atk:b.attack*g.gA,hp:b.maxHP*g.gH,def:b.defense,as:1};};
let margins=[];
for(const h of [D.heroes.find(x=>x.id===1).base,D.heroes.find(x=>x.id===2).base])
 for(const L of [1,3,5,10,15,20]){const P=pc(h,L);
  for(const e of D.enemies) for(const S of [1,3,5,10,15,20]){
    const E=ec(e.base,S);
    const a=CP1(P), b=CP1(E);
    margins.push(100*Math.abs(a-b)/Math.min(a,b));
  }}
margins.sort((x,y)=>x-y);
const below=(t)=>margins.filter(m=>m<t).length;
console.log(`  total matchups              ${margins.length}`);
console.log(`  margin < 1%                ${below(1)}  (${(100*below(1)/margins.length).toFixed(1)}%)`);
console.log(`  margin < 5%                ${below(5)}  (${(100*below(5)/margins.length).toFixed(1)}%)`);
console.log(`  margin < 10%               ${below(10)}  (${(100*below(10)/margins.length).toFixed(1)}%)`);
console.log(`  median margin              ${margins[Math.floor(margins.length/2)].toFixed(0)}%`);
console.log(`
 Real player-vs-enemy matchups are rarely close. The median gap is
 ${margins[Math.floor(margins.length/2)].toFixed(0)}%, far above the zone where rounding matters. That is why the
 408-duel test returned 100% - not because reversals are impossible, but
 because near-ties barely occur between a hero and an enemy.
`);
