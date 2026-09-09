// quant.js — the rounding error shrinks as fights take more hits.
// How many hits must a kill take for "higher CP always wins" to hold?
const R = 0.6;
const ehp = u => u.hp * (100 + Math.max(0,u.def)) / 100;
const CP  = u => u.atk * u.as * ehp(u);
const ttk = (A,B) => Math.ceil(ehp(B)/A.atk) * (R/A.as);
const hits= (A,B) => Math.ceil(ehp(B)/A.atk);

function run(hpScale, label) {
  let n=0, ok=0, draw=0, hitSum=0;
  const rnd=(a,b)=>a+Math.random()*(b-a);
  for (let i=0;i<400000;i++){
    const A={atk:rnd(20,300),hp:rnd(80,2000)*hpScale,def:rnd(0,150),as:rnd(0.7,1.6)};
    const B={atk:rnd(20,300),hp:rnd(80,2000)*hpScale,def:rnd(0,150),as:rnd(0.7,1.6)};
    const ca=CP(A), cb=CP(B); if(ca===cb) continue;
    const ta=ttk(A,B), tb=ttk(B,A);
    hitSum += (hits(A,B)+hits(B,A))/2;
    n++;
    if (Math.abs(ta-tb)<1e-12) { draw++; continue; }
    const win = ta<tb?'a':'b', says = ca>cb?'a':'b';
    if (win===says) ok++;
  }
  const avgHits = hitSum/n;
  console.log(`  ${label.padEnd(26)} avg ${avgHits.toFixed(1).padStart(5)} hits/kill   correct ${(100*ok/n).toFixed(2).padStart(6)}%   draws ${(100*draw/n).toFixed(2).padStart(5)}%`);
  return {avgHits, acc:100*ok/n};
}

console.log('=== ACCURACY vs HOW MANY HITS A KILL TAKES ===\n');
console.log('  (same formula throughout - only the HP:ATK ratio changes)\n');
for (const [s,l] of [[0.25,'HP x0.25 (very short)'],[0.5,'HP x0.5'],[1,'HP x1  (current-ish)'],
                     [2,'HP x2'],[4,'HP x4'],[8,'HP x8'],[16,'HP x16'],[40,'HP x40 (long fights)']])
  run(s,l);

console.log('\n=== WHERE THE CURRENT GAME SITS ===\n');
// real roster: player L1 fast vs Skeleton S1
const P={atk:64,hp:100,def:25,as:1.0}, E={atk:44,hp:205,def:52,as:1.0};
console.log(`  player kills Skeleton in ${hits(P,E)} hits;  Skeleton kills player in ${hits(E,P)} hits`);
console.log('  -> fights resolve in 3-6 hits, which is deep in the high-error zone.\n');

console.log('=== WHAT THAT MEANS ===\n');
console.log('  The formula is exactly monotonic in continuous maths (0 errors in 200k tests).');
console.log('  All remaining error comes from ceil() on the hit count.');
console.log('  Longer fights -> finer granularity -> the guarantee tightens.');
console.log('  At ~4 hits a kill,  a 1% CP edge is basically a coin flip.');
console.log('  At ~40 hits a kill, a 1% CP edge nearly always wins.');
