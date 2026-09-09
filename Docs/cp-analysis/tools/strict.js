// strict.js — does "1 more CP always wins" actually hold?
// Tests the proposed CP = ATK x AtkSpd x EffectiveHP under the PROPOSED combat model
// (cadence = recoveryTime / attackSpeed), including the integer hits-to-kill rounding.

const R = 0.6;                                   // recoveryTime
const dmg = (atk, def) => Math.max(1, atk * 100 / (100 + Math.max(0, def)));
const ehp = u => u.hp * (100 + Math.max(0, u.def)) / 100;
const CP  = u => u.atk * u.as * ehp(u);
// time for A to kill B
const ttk = (A, B) => Math.ceil(ehp(B) / A.atk) * (R / A.as);

function duel(A, B) {
  const ta = ttk(A, B), tb = ttk(B, A);
  if (Math.abs(ta - tb) < 1e-12) return 'draw';   // simultaneous kill
  return ta < tb ? 'a' : 'b';
}

// ---------- 1) continuous check: is the formula exactly monotonic without rounding? ----------
console.log('=== 1) WITHOUT integer rounding, is CP exactly monotonic? ===\n');
const ttkC = (A,B) => (ehp(B)/A.atk) * (R/A.as);   // no ceil
let contBad = 0, contN = 0;
for (let i=0;i<200000;i++){
  const rnd=(a,b)=>a+Math.random()*(b-a);
  const A={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const B={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const ta=ttkC(A,B), tb=ttkC(B,A);
  if (Math.abs(ta-tb)<1e-12) continue;
  contN++;
  const win = ta<tb?'a':'b';
  const cpSays = CP(A)>CP(B)?'a':'b';
  if (win!==cpSays) contBad++;
}
console.log(`  ${contN.toLocaleString()} random duels, no rounding`);
console.log(`  CP wrong: ${contBad}   -> ${contBad===0?'EXACTLY MONOTONIC (proven algebraically too)':'NOT monotonic'}\n`);

// ---------- 2) with integer hits: how often does the higher CP fail to win? ----------
console.log('=== 2) WITH integer hits-to-kill (the real game), by CP margin ===\n');
const buckets = [[0,0.1],[0.1,0.5],[0.5,1],[1,2],[2,5],[5,10],[10,100]];
const stats = buckets.map(()=>({n:0,win:0,draw:0,lose:0}));
for (let i=0;i<600000;i++){
  const rnd=(a,b)=>a+Math.random()*(b-a);
  const A={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const B={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const ca=CP(A), cb=CP(B);
  if (ca===cb) continue;
  const marginPct = 100*Math.abs(ca-cb)/Math.min(ca,cb);
  const bi = buckets.findIndex(([lo,hi])=>marginPct>=lo&&marginPct<hi);
  if (bi<0) continue;
  const higher = ca>cb ? 'a':'b';
  const r = duel(A,B);
  const s = stats[bi];
  s.n++;
  if (r==='draw') s.draw++; else if (r===higher) s.win++; else s.lose++;
}
console.log('  CP margin      duels     higher CP wins   draw    higher CP LOSES');
console.log('  ------------------------------------------------------------------');
buckets.forEach(([lo,hi],i)=>{
  const s=stats[i]; if(!s.n) return;
  const p=v=>(100*v/s.n).toFixed(2).padStart(6);
  console.log(`  ${(lo+'-'+hi+'%').padEnd(12)} ${String(s.n).padStart(7)}    ${p(s.win)}%      ${p(s.draw)}%    ${p(s.lose)}%`);
});

// ---------- 3) the same, but on the REAL roster values ----------
console.log('\n=== 3) WHY rounding breaks it: a concrete example ===\n');
const A={atk:100,hp:300,def:50,as:1.0};
const B={atk:99, hp:300,def:50,as:1.0};
console.log(`  A: ATK ${A.atk} HP ${A.hp} DEF ${A.def} AS ${A.as}  ->  CP ${CP(A).toFixed(0)}`);
console.log(`  B: ATK ${B.atk} HP ${B.hp} DEF ${B.def} AS ${B.as}  ->  CP ${CP(B).toFixed(0)}   (A leads by ${(CP(A)-CP(B)).toFixed(0)})`);
console.log(`  A needs ceil(${ehp(B).toFixed(0)}/${A.atk}) = ${Math.ceil(ehp(B)/A.atk)} hits -> ${ttk(A,B).toFixed(3)}s`);
console.log(`  B needs ceil(${ehp(A).toFixed(0)}/${B.atk}) = ${Math.ceil(ehp(A)/B.atk)} hits -> ${ttk(B,A).toFixed(3)}s`);
console.log(`  result: ${duel(A,B)==='draw'?'DRAW - both die on the same swing':'winner '+duel(A,B)}`);
console.log('\n  Hits are whole numbers. A 1-CP edge is invisible if it does not change');
console.log('  the hit COUNT, so both units land their killing blow on the same tick.');

// ---------- 4) how much margin guarantees a win? ----------
console.log('\n=== 4) MARGIN NEEDED FOR A GUARANTEED WIN ===\n');
let worstLose = 0;
for (let i=0;i<600000;i++){
  const rnd=(a,b)=>a+Math.random()*(b-a);
  const A={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const B={atk:rnd(20,300),hp:rnd(80,2000),def:rnd(0,150),as:rnd(0.7,1.6)};
  const ca=CP(A), cb=CP(B); if(ca===cb) continue;
  const higher = ca>cb?'a':'b';
  const r=duel(A,B);
  if (r!=='draw' && r!==higher) {
    const m = 100*Math.abs(ca-cb)/Math.min(ca,cb);
    if (m>worstLose) worstLose=m;
  }
}
console.log(`  Largest CP margin where the higher-CP unit still LOST: ${worstLose.toFixed(1)}%`);
console.log('  (caused purely by integer hit counts, not by the formula)');
